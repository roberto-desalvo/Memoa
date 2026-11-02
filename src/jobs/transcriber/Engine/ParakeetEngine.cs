using System.Text;
using Memoa.Shared.Pipeline;
using Memoa.Transcriber.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SherpaOnnx;
using Whisper.net;

namespace Memoa.Transcriber.Engine;

/// <summary>
/// NVIDIA Parakeet TDT 0.6B v3 (int8) tramite sherpa-onnx, su CPU, con i modelli inclusi nell'immagine.
/// Riconosce italiano e inglese e mette punteggiatura e maiuscole, ma non restituisce la lingua: la
/// rileva Whisper tiny sui primi 30 secondi, scegliendo tra quelle supportate; se la probabilità è sotto
/// la soglia si usa quella dell'account. La VAD Silero divide l'audio in segmenti di parlato, trascritti
/// uno alla volta: il modello ONNX usa l'attention completa, quindi la memoria cresce con la durata.
/// </summary>
public sealed class ParakeetEngine(Ffmpeg ffmpeg, IOptions<ParakeetOptions> options, ILogger<ParakeetEngine> logger)
    : ITranscriptionEngine, IDisposable
{
    /// <summary>Parakeet, Silero e Whisper lavorano su campioni mono a 16 kHz.</summary>
    private const int SampleRate = 16_000;

    /// <summary>Il rilevamento della lingua usa solo la prima finestra di 30 secondi.</summary>
    private const int DetectionSamples = 30 * SampleRate;

    /// <summary>Campioni per chiamata della VAD Silero (v4 e v5).</summary>
    private const int VadWindowSize = 512;

    private static readonly string[] CandidateLanguages = [.. TranscriptionRequest.SupportedLanguages];

    // Modelli caricati solo se c'è un messaggio da elaborare.
    private OfflineRecognizer? recognizer;
    private WhisperFactory? languageFactory;

    public string Name => "sherpa-onnx/parakeet-tdt-0.6b-v3-int8";

    private int Threads => options.Value.Threads ?? Environment.ProcessorCount;

    public async Task<TranscriptionResult> TranscribeAsync(string audioPath, string fallbackLanguage, CancellationToken cancellationToken)
    {
        float[] samples;
        try
        {
            samples = await ffmpeg.DecodeToPcmAsync(audioPath, SampleRate, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new TranscriptionException(Transcript.AudioDecodeFailed, "Decodifica in PCM non riuscita.", ex);
        }

        if (samples.Length == 0)
        {
            return new TranscriptionResult(string.Empty, fallbackLanguage, null, null);
        }

        LoadModels();

        var (detected, probability) = DetectLanguage(samples);
        var language = detected is not null && probability >= options.Value.LanguageProbabilityThreshold
            ? detected
            : fallbackLanguage;

        var text = Recognize(samples, cancellationToken);
        return new TranscriptionResult(text, language, detected, probability);
    }

    private (string? Language, float Probability) DetectLanguage(float[] samples)
    {
        try
        {
            using var detector = languageFactory!.CreateBuilder().WithThreads(Threads).Build();
            return detector.DetectLanguageWithProbability(
                samples.AsSpan(0, Math.Min(samples.Length, DetectionSamples)),
                CandidateLanguages);
        }
        catch (WhisperModelLoadException ex)
        {
            // Whisper.net legge il modello in modo differito, anche al primo uso del processore.
            throw new TranscriptionException(Transcript.ModelLoadFailed, $"Caricamento del modello {options.Value.LanguageModelPath} non riuscito.", ex);
        }
    }

    /// <summary>
    /// Passa l'audio alla VAD a finestre e trascrive ogni segmento di parlato appena è completo. I
    /// segmenti sono separati da pause, quindi si uniscono con uno spazio.
    /// </summary>
    private string Recognize(float[] samples, CancellationToken cancellationToken)
    {
        var vadConfig = new VadModelConfig { SampleRate = SampleRate, NumThreads = 1 };
        vadConfig.SileroVad.Model = options.Value.VadModelPath;
        vadConfig.SileroVad.WindowSize = VadWindowSize;
        // Soglie più basse dei predefiniti, come negli esempi di sherpa-onnx per Parakeet: meglio un
        // segmento di silenzio in più che una parola persa.
        vadConfig.SileroVad.Threshold = 0.2F;
        vadConfig.SileroVad.MinSpeechDuration = 0.2F;
        vadConfig.SileroVad.MinSilenceDuration = 0.5F;
        vadConfig.SileroVad.MaxSpeechDuration = options.Value.MaxSpeechSeconds;

        // Il buffer deve contenere almeno un segmento della durata massima.
        using var vad = new VoiceActivityDetector(vadConfig, bufferSizeInSeconds: 2 * options.Value.MaxSpeechSeconds + 10);
        var text = new StringBuilder();
        var window = new float[VadWindowSize];

        // Gli ultimi campioni che non riempiono una finestra (meno di 32 ms) non contengono parlato utile.
        for (var start = 0; start + VadWindowSize <= samples.Length; start += VadWindowSize)
        {
            Array.Copy(samples, start, window, 0, VadWindowSize);
            vad.AcceptWaveform(window);
            DecodeSegments(vad, text, cancellationToken);
        }

        vad.Flush();
        DecodeSegments(vad, text, cancellationToken);
        return text.ToString().Trim();
    }

    private void DecodeSegments(VoiceActivityDetector vad, StringBuilder text, CancellationToken cancellationToken)
    {
        while (!vad.IsEmpty())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var stream = recognizer!.CreateStream();
            stream.AcceptWaveform(SampleRate, vad.Front().Samples);
            recognizer.Decode(stream);
            vad.Pop();

            var segment = stream.Result.Text.Trim();
            if (segment.Length > 0)
            {
                text.Append(segment).Append(' ');
            }
        }
    }

    /// <summary>
    /// Controlla i file dei modelli e li carica alla prima trascrizione. sherpa-onnx non segnala in modo
    /// affidabile un file mancante: i controlli precedono il caricamento, con un log di dimensione e memoria.
    /// </summary>
    private void LoadModels()
    {
        if (recognizer is not null)
        {
            return;
        }

        var directory = options.Value.ModelDirectory;
        var encoder = Path.Combine(directory, "encoder.int8.onnx");
        var decoder = Path.Combine(directory, "decoder.int8.onnx");
        var joiner = Path.Combine(directory, "joiner.int8.onnx");
        var tokens = Path.Combine(directory, "tokens.txt");

        foreach (var path in new[] { encoder, decoder, joiner, tokens, options.Value.VadModelPath, options.Value.LanguageModelPath })
        {
            try
            {
                // Exists è false anche se manca il permesso sulla cartella: l'apertura riporta la causa reale.
                File.OpenRead(path).Dispose();
            }
            catch (Exception ex)
            {
                throw new TranscriptionException(Transcript.ModelLoadFailed, $"Modello non leggibile in {path}.", ex);
            }
        }

        logger.LogInformation(
            "Caricamento dei modelli da {ModelDirectory}: encoder {EncoderBytes} byte, memoria disponibile {AvailableMemoryBytes} byte, {ProcessorCount} vCPU, {Threads} thread",
            directory, new FileInfo(encoder).Length, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, Environment.ProcessorCount, Threads);

        try
        {
            var config = new OfflineRecognizerConfig();
            config.ModelConfig.Transducer.Encoder = encoder;
            config.ModelConfig.Transducer.Decoder = decoder;
            config.ModelConfig.Transducer.Joiner = joiner;
            config.ModelConfig.Tokens = tokens;
            config.ModelConfig.ModelType = "nemo_transducer";
            config.ModelConfig.NumThreads = Threads;
            config.ModelConfig.Provider = "cpu";
            config.ModelConfig.Debug = 0;
            config.DecodingMethod = "greedy_search";

            recognizer = new OfflineRecognizer(config);
            languageFactory = WhisperFactory.FromPath(options.Value.LanguageModelPath);
        }
        catch (Exception ex)
        {
            throw new TranscriptionException(Transcript.ModelLoadFailed, $"Caricamento dei modelli da {directory} non riuscito.", ex);
        }
    }

    public void Dispose()
    {
        recognizer?.Dispose();
        languageFactory?.Dispose();
    }
}
