using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace Memoa.Normalizer.Audio;

/// <summary>Esegue ffprobe e ffmpeg installati nell'immagine.</summary>
public sealed class Ffmpeg(IOptions<JobOptions> options)
{
    /// <summary>
    /// Parametri della copia normalizzata: Ogg Opus mono a 32 kbps VBR (circa 0,25 MB al minuto),
    /// ottimizzato per il parlato. Metadati e capitoli del file caricato non vengono copiati.
    /// </summary>
    private static readonly string[] OpusArguments =
    [
        "-map", "0:a:0",
        "-ac", "1",
        "-c:a", "libopus",
        "-b:a", "32k",
        "-vbr", "on",
        "-application", "voip",
        "-map_metadata", "-1",
        "-map_chapters", "-1",
    ];

    /// <summary>Null se ffprobe non riconosce il file come contenuto multimediale.</summary>
    public async Task<AudioProbe?> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        var result = await RunAsync(options.Value.FfprobePath,
            ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", path],
            cancellationToken);

        return result.ExitCode == 0 ? AudioProbe.Parse(result.StandardOutput) : null;
    }

    /// <summary>
    /// Converte la prima traccia audio. La conversione si interrompe poco oltre la durata massima:
    /// i file senza durata nell'header (WebM del browser) vengono poi scartati controllando la copia.
    /// </summary>
    public async Task TranscodeToOpusAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        var limit = (AudioValidation.MaxDuration.TotalSeconds + 1).ToString(CultureInfo.InvariantCulture);

        string[] arguments =
        [
            "-nostdin", "-hide_banner", "-v", "error", "-y",
            "-i", inputPath,
            .. OpusArguments,
            "-t", limit,
            "-f", "ogg",
            outputPath,
        ];

        var result = await RunAsync(options.Value.FfmpegPath, arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            // Lo stderr di ffmpeg può riportare i metadati del file: nell'eccezione solo il codice di uscita.
            throw new InvalidOperationException($"ffmpeg terminato con codice {result.ExitCode}.");
        }
    }

    private static async Task<ProcessResult> RunAsync(string fileName, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Impossibile avviare {fileName}.");

        // Lettura in parallelo di stdout e stderr, altrimenti un buffer pieno blocca il processo.
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        // Lo stderr non viene usato: può riportare i metadati del file.
        await standardError;
        return new ProcessResult(process.ExitCode, await standardOutput);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput);
}
