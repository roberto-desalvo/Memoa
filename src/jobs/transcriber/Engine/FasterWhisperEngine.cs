using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Memoa.Shared.Pipeline;
using Memoa.Transcriber.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Transcriber.Engine;

/// <summary>
/// Whisper large-v3-turbo con faster-whisper (CTranslate2 int8 su CPU), in uno script Python
/// (FasterWhisper/transcribe.py) avviato a ogni trascrizione: il worker resta in .NET e ffmpeg decodifica
/// l'audio come per gli altri motori. Lo script rileva la lingua tra quelle supportate (sotto la soglia
/// vale quella dell'account) e trascrive con il prompt della lingua e la VAD Silero inclusa.
/// Richiesta in JSON su stdin, risultato in JSON su stdout; su stderr solo il tipo dell'eccezione.
/// </summary>
public sealed class FasterWhisperEngine(Ffmpeg ffmpeg, IOptions<FasterWhisperOptions> options, ILogger<FasterWhisperEngine> logger)
    : ITranscriptionEngine
{
    /// <summary>Whisper lavora su campioni mono a 16 kHz.</summary>
    private const int SampleRate = 16_000;

    /// <summary>Codice di uscita dello script se il modello non si carica.</summary>
    private const int ModelLoadFailedExitCode = 3;

    public string Name => "faster-whisper/large-v3-turbo-int8";

    public async Task<TranscriptionResult> TranscribeAsync(string audioPath, string fallbackLanguage, CancellationToken cancellationToken)
    {
        string pcmPath;
        try
        {
            pcmPath = await ffmpeg.DecodeToPcmFileAsync(audioPath, SampleRate, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new TranscriptionException(Transcript.AudioDecodeFailed, "Decodifica in PCM non riuscita.", ex);
        }

        if (new FileInfo(pcmPath).Length == 0)
        {
            return new TranscriptionResult(string.Empty, fallbackLanguage, null, null);
        }

        var settings = options.Value;
        CheckFiles(settings);

        var threads = settings.Threads ?? Environment.ProcessorCount;
        logger.LogInformation(
            "Avvio di faster-whisper con il modello {ModelDirectory}: memoria disponibile {AvailableMemoryBytes} byte, {ProcessorCount} vCPU, {Threads} thread, beam {BeamSize}, batch {BatchSize}",
            settings.ModelDirectory, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, Environment.ProcessorCount, threads, settings.BeamSize, settings.BatchSize);

        var request = JsonSerializer.Serialize(new
        {
            pcmPath,
            modelDirectory = settings.ModelDirectory,
            threads,
            beamSize = settings.BeamSize,
            batchSize = settings.BatchSize,
            languages = TranscriptionRequest.SupportedLanguages,
            fallbackLanguage,
            probabilityThreshold = settings.LanguageProbabilityThreshold,
            prompts = settings.Prompts,
        }, JsonSerializerOptions.Web);

        var (exitCode, output, error) = await RunScriptAsync(settings, request, cancellationToken);
        if (exitCode == ModelLoadFailedExitCode)
        {
            throw new TranscriptionException(Transcript.ModelLoadFailed, $"Caricamento del modello {settings.ModelDirectory} non riuscito ({error}).");
        }
        if (exitCode != 0)
        {
            // Lo stderr contiene solo il tipo dell'eccezione Python, mai il testo.
            throw new InvalidOperationException($"faster-whisper terminato con codice {exitCode} ({error}).");
        }

        var result = JsonSerializer.Deserialize<ScriptResult>(output, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Risultato di faster-whisper vuoto.");
        return new TranscriptionResult(result.Text, result.Language, result.DetectedLanguage, result.LanguageProbability);
    }

    /// <summary>Errori deterministici: si ripeterebbero uguali a ogni tentativo con la stessa immagine.</summary>
    private static void CheckFiles(FasterWhisperOptions settings)
    {
        foreach (var path in new[] { settings.ScriptPath, Path.Combine(settings.ModelDirectory, "model.bin") })
        {
            try
            {
                // Exists è false anche se manca il permesso sulla cartella: l'apertura riporta la causa reale.
                File.OpenRead(path).Dispose();
            }
            catch (Exception ex)
            {
                throw new TranscriptionException(Transcript.ModelLoadFailed, $"File non leggibile in {path}.", ex);
            }
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunScriptAsync(
        FasterWhisperOptions settings,
        string request,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(settings.PythonPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        // -I: modalità isolata, senza variabili PYTHON* né la cartella corrente nel percorso dei moduli.
        // -X utf8: stdin e stdout in UTF-8 anche dove la codifica predefinita è un'altra (es. Windows).
        startInfo.ArgumentList.Add("-I");
        startInfo.ArgumentList.Add("-X");
        startInfo.ArgumentList.Add("utf8");
        startInfo.ArgumentList.Add(settings.ScriptPath);
        // Il modello è nell'immagine: nessuna richiesta a Hugging Face.
        startInfo.Environment["HF_HUB_OFFLINE"] = "1";

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Impossibile avviare {settings.PythonPath}.");
        }
        catch (Win32Exception ex)
        {
            throw new TranscriptionException(Transcript.ModelLoadFailed, $"Interprete Python non trovato in {settings.PythonPath}.", ex);
        }

        using (process)
        {
            // Lettura in parallelo di stdout e stderr, altrimenti un buffer pieno blocca il processo.
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await process.StandardInput.WriteAsync(request.AsMemory(), cancellationToken);
                process.StandardInput.Close();
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            await Task.WhenAll(output, error);
            return (process.ExitCode, output.Result, error.Result.Trim());
        }
    }

    private sealed record ScriptResult(string Text, string Language, string? DetectedLanguage, float LanguageProbability);
}
