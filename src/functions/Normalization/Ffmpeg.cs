using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Normalization;

/// <summary>Configurazione di ffmpeg (sezione "Ffmpeg", facoltativa).</summary>
public sealed class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";

    /// <summary>
    /// Percorso dell'eseguibile. Se vuoto si usa la build statica inclusa nel pacchetto dalla pipeline
    /// (<see cref="Ffmpeg.BundledPath"/>) oppure, se manca (sviluppo locale), "ffmpeg" dal PATH.
    /// </summary>
    public string? Path { get; set; }
}

/// <summary>
/// Esegue ffmpeg, sia per leggere le informazioni sul file (al posto di ffprobe, per non raddoppiare lo
/// spazio occupato nel pacchetto) sia per la conversione.
/// </summary>
public sealed class Ffmpeg
{
    /// <summary>Posizione nel pacchetto di deploy, relativa alla cartella dell'applicazione.</summary>
    public static readonly string BundledPath = System.IO.Path.Combine("tools", "ffmpeg", "ffmpeg");

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

    private readonly Lazy<string> executable;

    public Ffmpeg(IOptions<FfmpegOptions> options)
    {
        executable = new Lazy<string>(() => ResolveExecutable(options.Value.Path), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Null se ffmpeg non riconosce il file come contenuto multimediale.</summary>
    public async Task<AudioProbe?> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        // Senza output ffmpeg termina con un codice d'errore dopo aver stampato il riepilogo dell'input:
        // il codice di uscita non conta, conta il riepilogo.
        var result = await RunAsync(["-nostdin", "-hide_banner", "-i", path], cancellationToken);
        return AudioProbe.Parse(result.StandardError);
    }

    /// <summary>
    /// Converte la prima traccia audio. La conversione si interrompe poco oltre la durata massima:
    /// i file senza durata nell'header (WebM del browser) vengono poi scartati controllando la copia.
    /// </summary>
    public async Task TranscodeToOpusAsync(string inputPath, string outputPath, TimeSpan maxDuration, CancellationToken cancellationToken)
    {
        var limit = (maxDuration.TotalSeconds + 1).ToString(CultureInfo.InvariantCulture);

        string[] arguments =
        [
            "-nostdin", "-hide_banner", "-v", "error", "-y",
            "-i", inputPath,
            .. OpusArguments,
            "-t", limit,
            "-f", "ogg",
            outputPath,
        ];

        var result = await RunAsync(arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            // Lo stderr di ffmpeg può riportare i metadati del file: nell'eccezione solo il codice di uscita.
            throw new InvalidOperationException($"ffmpeg terminato con codice {result.ExitCode}.");
        }
    }

    private async Task<ProcessResult> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable.Value)
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
            ?? throw new InvalidOperationException("Impossibile avviare ffmpeg.");

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

        await standardOutput;
        // Lo stderr può riportare i metadati del file: si interpreta, mai nei log.
        return new ProcessResult(process.ExitCode, await standardError);
    }

    /// <summary>
    /// Il pacchetto di deploy (zip) può perdere il permesso di esecuzione e la cartella dell'applicazione
    /// può essere di sola lettura: in quel caso si usa una copia eseguibile nella cartella temporanea.
    /// </summary>
    private static string ResolveExecutable(string? configuredPath)
    {
        if (!string.IsNullOrEmpty(configuredPath))
        {
            return configuredPath;
        }

        var bundled = System.IO.Path.Combine(AppContext.BaseDirectory, BundledPath);
        if (OperatingSystem.IsWindows() || !File.Exists(bundled))
        {
            return "ffmpeg";
        }

        const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        if ((File.GetUnixFileMode(bundled) & anyExecute) != 0)
        {
            return bundled;
        }

        var copy = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "memoa-ffmpeg", "ffmpeg");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(copy)!);
        File.Copy(bundled, copy, overwrite: true);
        File.SetUnixFileMode(copy, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return copy;
    }

    private sealed record ProcessResult(int ExitCode, string StandardError);
}
