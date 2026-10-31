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

    /// <summary>
    /// Whisper.net legge il modello alla creazione del primo processore, non in FromPath: gli errori di
    /// caricamento (WhisperModelLoadException) arrivano da Build.
    /// </summary>
    private WhisperProcessor CreateProcessor()
    {
        var modelPath = options.Value.ModelPath;
        var model = new FileInfo(modelPath);
        if (!model.Exists)
        {
            throw new TranscriptionException(Transcript.ModelLoadFailed, $"Modello non trovato in {modelPath}.");
        }

        // Valori tecnici per distinguere un file troncato o non GGML da memoria insufficiente.
        logger.LogInformation(
            "Caricamento del modello {ModelPath}: {ModelBytes} byte, memoria disponibile {AvailableMemoryBytes} byte, {ProcessorCount} vCPU",
            modelPath, model.Length, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, Environment.ProcessorCount);

        try
        {
            // In un container Environment.ProcessorCount rispetta il limite di vCPU del job.
            return factory.Value.CreateBuilder()
                .WithThreads(Environment.ProcessorCount)
                .Build();
        }
        catch (Exception ex)
        {
            throw new TranscriptionException(Transcript.ModelLoadFailed, $"Caricamento del modello {modelPath} non riuscito.", ex);
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
