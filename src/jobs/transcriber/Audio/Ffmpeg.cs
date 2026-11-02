using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;

namespace Memoa.Transcriber.Audio;

/// <summary>Esegue ffmpeg installato nell'immagine.</summary>
public sealed class Ffmpeg(IOptions<JobOptions> options)
{
    /// <summary>
    /// Decodifica la prima traccia audio in campioni PCM float32 mono, il formato di ingresso dei
    /// motori di trascrizione. Il file intermedio viene scritto accanto all'ingresso, nella cartella di
    /// lavoro del job.
    /// </summary>
    public async Task<float[]> DecodeToPcmAsync(string inputPath, int sampleRate, CancellationToken cancellationToken)
    {
        var outputPath = await DecodeToPcmFileAsync(inputPath, sampleRate, cancellationToken);

        // f32le è little endian come le piattaforme supportate: i byte si leggono direttamente nell'array.
        await using var stream = File.OpenRead(outputPath);
        var samples = new float[stream.Length / sizeof(float)];
        stream.ReadExactly(MemoryMarshal.AsBytes(samples.AsSpan()));
        return samples;
    }

    /// <summary>
    /// Come <see cref="DecodeToPcmAsync"/>, ma restituisce il percorso del file PCM (f32le) senza
    /// leggerlo, per i motori che girano in un altro processo.
    /// </summary>
    public async Task<string> DecodeToPcmFileAsync(string inputPath, int sampleRate, CancellationToken cancellationToken)
    {
        var outputPath = Path.ChangeExtension(inputPath, ".pcm");

        string[] arguments =
        [
            "-nostdin", "-hide_banner", "-v", "error", "-y",
            "-i", inputPath,
            "-map", "0:a:0",
            "-ac", "1",
            "-ar", sampleRate.ToString(CultureInfo.InvariantCulture),
            "-f", "f32le",
            outputPath,
        ];

        var exitCode = await RunAsync(options.Value.FfmpegPath, arguments, cancellationToken);
        if (exitCode != 0)
        {
            // Lo stderr di ffmpeg può riportare i metadati del file: nell'eccezione solo il codice di uscita.
            throw new InvalidOperationException($"ffmpeg terminato con codice {exitCode}.");
        }

        return outputPath;
    }

    private static async Task<int> RunAsync(string fileName, IEnumerable<string> arguments, CancellationToken cancellationToken)
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

        // Lettura in parallelo di stdout e stderr, altrimenti un buffer pieno blocca il processo. Il
        // contenuto non viene usato: lo stderr può riportare i metadati del file.
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

        await Task.WhenAll(standardOutput, standardError);
        return process.ExitCode;
    }
}
