using System.Text;
using Memoa.Shared.Pipeline;
using Memoa.Transcriber.Audio;
using Microsoft.Extensions.Options;
using Whisper.net;

namespace Memoa.Transcriber.Engine;

/// <summary>
/// Whisper self-hosted tramite Whisper.net (whisper.cpp su CPU), con il modello GGML incluso nell'immagine.
/// La lingua viene rilevata tra quelle supportate; se la probabilità è sotto la soglia si usa quella
/// dell'account.
/// </summary>
public sealed class WhisperEngine(Ffmpeg ffmpeg, IOptions<WhisperOptions> options) : ITranscriptionEngine, IDisposable
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
        var samples = await ffmpeg.DecodeToPcmAsync(audioPath, SampleRate, cancellationToken);
        if (samples.Length == 0)
        {
            return new TranscriptionResult(string.Empty, fallbackLanguage, null, null);
        }

        // In un container Environment.ProcessorCount rispetta il limite di vCPU del job.
        await using var processor = factory.Value.CreateBuilder()
            .WithThreads(Environment.ProcessorCount)
            .Build();

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

    public void Dispose()
    {
        if (factory.IsValueCreated)
        {
            factory.Value.Dispose();
        }
    }
}
