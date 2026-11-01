using System.Globalization;
using System.Text.RegularExpressions;

namespace Memoa.Functions.Normalization;

/// <summary>Le informazioni sul file usate per la validazione, lette dall'output di <c>ffmpeg -i</c>.</summary>
public sealed partial record AudioProbe(string FormatName, int AudioStreams, bool HasVideo, double? DurationSeconds)
{
    /// <summary>
    /// Interpreta il riepilogo che ffmpeg scrive su stderr per il primo input (senza ffprobe, che non è nel
    /// pacchetto). Null se ffmpeg non riconosce il file come contenuto multimediale.
    /// I valori multiriga dei metadati vengono stampati da ffmpeg come righe di continuazione ("  : ..."),
    /// quindi non possono simulare le righe "Duration:" o "Stream #0:".
    /// </summary>
    public static AudioProbe? Parse(string ffmpegOutput)
    {
        var lines = ffmpegOutput.Split('\n', StringSplitOptions.TrimEntries);

        string? formatName = null;
        double? duration = null;
        var audioStreams = 0;
        var hasVideo = false;

        foreach (var line in lines)
        {
            if (formatName is null)
            {
                var input = InputPattern().Match(line);
                if (input.Success)
                {
                    formatName = input.Groups["format"].Value;
                }
                continue;
            }

            // Solo il primo input: ffmpeg riepiloga poi l'eventuale output.
            if (line.StartsWith("Output #", StringComparison.Ordinal))
            {
                break;
            }

            var durationMatch = DurationPattern().Match(line);
            if (durationMatch.Success && duration is null)
            {
                // Assente ("N/A") per le registrazioni WebM del browser, che non hanno la durata nell'header.
                duration = ParseDuration(durationMatch);
                continue;
            }

            var stream = StreamPattern().Match(line);
            if (stream.Success)
            {
                switch (stream.Groups["type"].Value)
                {
                    case "Audio":
                        audioStreams++;
                        break;
                    // La copertina di un MP3 o M4A è uno stream video "(attached pic)".
                    case "Video" when !line.Contains("(attached pic)", StringComparison.Ordinal):
                        hasVideo = true;
                        break;
                }
            }
        }

        return formatName is null ? null : new AudioProbe(formatName, audioStreams, hasVideo, duration);
    }

    private static double ParseDuration(Match match) =>
        int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture) * 3600
        + int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) * 60
        + double.Parse(match.Groups["s"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);

    // "Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'input':": i nomi dei formati non contengono ", ".
    [GeneratedRegex(@"^Input #0, (?<format>.+?), from '")]
    private static partial Regex InputPattern();

    [GeneratedRegex(@"^Duration: (?<h>\d+):(?<m>\d{2}):(?<s>\d{2}(?:\.\d+)?),")]
    private static partial Regex DurationPattern();

    // "Stream #0:0[0x1](und): Audio: aac (LC) ...", "Stream #0:1: Video: mjpeg ... (attached pic)".
    [GeneratedRegex(@"^Stream #0:\d+\S*: (?<type>\w+):")]
    private static partial Regex StreamPattern();
}
