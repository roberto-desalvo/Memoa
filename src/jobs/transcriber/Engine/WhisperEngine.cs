using System.Text;
using Memoa.Shared.Pipeline;
using Memoa.Transcriber.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Whisper.net;

namespace Memoa.Transcriber.Engine;

/// <summary>
/// Whisper self-hosted tramite Whisper.net (whisper.cpp su CPU), con il modello GGML incluso nell'immagine.
/// La lingua viene rilevata tra quelle supportate; se la probabilità è sotto la soglia si usa quella
/// dell'account.
/// </summary>
public sealed class WhisperEngine(Ffmpeg ffmpeg, IOptions<WhisperOptions> options, ILogger<WhisperEngine> logger)
    : ITranscriptionEngine, IDisposable
{
    /// <summary>Whisper lavora su campioni mono a 16 kHz.</summary>
    private const int SampleRate = 16_000;

    /// <summary>Il rilevamento della lingua usa solo la prima finestra di 30 secondi.</summary>
    private const int DetectionSamples = 30 * SampleRate;

    private static readonly string[] CandidateLanguages = [.. TranscriptionRequest.SupportedLanguages];

    // Il caricamento del modello (circa 550 MB) avviene solo se c'è un messaggio da elaborare.
    private readonly Lazy<WhisperFactory> factory = new(() => WhisperFactory.FromPath(options.Value.ModelPath));

    public string Name => $"whisper.net/{Path.GetFileNameWithoutExtension(options.Value.ModelPath)}";

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

        try
        {
            await using var processor = CreateProcessor();

            var (detected, probability) = processor.DetectLanguageWithProbability(
                samples.AsSpan(0, Math.Min(samples.Length, DetectionSamples)),
                CandidateLanguages);

            var language = detected is not null && probability >= options.Value.LanguageProbabilityThreshold
                ? detected
                : fallbackLanguage;
            processor.ChangeLanguage(language);

            var text = new StringBuilder();
            await foreach (var segment in processor.ProcessAsync(samples, cancellationToken))
            {
                text.Append(segment.Text);
            }

            return new TranscriptionResult(text.ToString().Trim(), language, detected, probability);
        }
        catch (WhisperModelLoadException ex)
        {
            // Whisper.net può leggere il modello in modo differito, anche al primo uso del processore:
            // si riconosce dal tipo dell'eccezione, ovunque sia sollevata.
            throw new TranscriptionException(Transcript.ModelLoadFailed, $"Caricamento del modello {options.Value.ModelPath} non riuscito.", ex);
        }
    }

    /// <summary>
    /// Whisper.net non legge il modello in FromPath ma in modo differito: i controlli sul file e il log
    /// con dimensione e memoria precedono il primo uso. Le WhisperModelLoadException successive le
    /// riconosce <see cref="TranscribeAsync"/>.
    /// </summary>
    private WhisperProcessor CreateProcessor()
    {
        var modelPath = options.Value.ModelPath;
        var model = new FileInfo(modelPath);
        if (!model.Exists)
        {
            // Exists è false anche se manca il permesso sulla cartella: l'apertura del file riporta la
            // causa reale (FileNotFound, DirectoryNotFound, UnauthorizedAccess) come eccezione interna.
            Exception? cause = null;
            try
            {
                File.OpenRead(modelPath).Dispose();
            }
            catch (Exception ex)
            {
                cause = ex;
            }

            throw new TranscriptionException(Transcript.ModelLoadFailed, $"Modello non trovato in {modelPath}.", cause);
        }

        var threads = options.Value.Threads ?? Environment.ProcessorCount;

        // Valori tecnici per distinguere un file troncato o non GGML da memoria insufficiente, e per
        // confrontare le vCPU viste dal processo con il limite del container (cpu.max) e le istruzioni
        // CPU usate da whisper.cpp.
        logger.LogInformation(
            "Caricamento del modello {ModelPath}: {ModelBytes} byte, memoria disponibile {AvailableMemoryBytes} byte, {ProcessorCount} vCPU, cpu.max {CpuMax}, {Threads} thread",
            modelPath, model.Length, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, Environment.ProcessorCount, ReadCpuMax(), threads);

        try
        {
            var processor = factory.Value.CreateBuilder()
                .WithThreads(threads)
                .Build();
            logger.LogInformation("Runtime di whisper.cpp: {RuntimeInfo}", WhisperFactory.GetRuntimeInfo());
            return processor;
        }
        catch (Exception ex)
        {
            throw new TranscriptionException(Transcript.ModelLoadFailed, $"Caricamento del modello {modelPath} non riuscito.", ex);
        }
    }

    /// <summary>Quota CPU del container (cgroup v2), es. "200000 100000" per 2 vCPU o "max 100000".</summary>
    private static string ReadCpuMax()
    {
        try
        {
            return File.ReadAllText("/sys/fs/cgroup/cpu.max").Trim();
        }
        catch (Exception)
        {
            return "n/d";
        }
    }

    public void Dispose()
    {
        if (factory.IsValueCreated)
        {
            factory.Value.Dispose();
        }
    }
}
