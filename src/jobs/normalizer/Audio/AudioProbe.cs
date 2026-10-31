using System.Globalization;
using System.Text.Json;

namespace Memoa.Normalizer.Audio;

/// <summary>Le informazioni dell'output JSON di ffprobe usate per la validazione.</summary>
public sealed record AudioProbe(string FormatName, int AudioStreams, bool HasVideo, double? DurationSeconds)
{
    /// <summary>Interpreta l'output di <c>ffprobe -print_format json -show_format -show_streams</c>.</summary>
    public static AudioProbe Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var audioStreams = 0;
        var hasVideo = false;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                switch (GetString(stream, "codec_type"))
                {
                    case "audio":
                        audioStreams++;
                        break;
                    // La copertina di un MP3 o M4A è uno stream video con disposition.attached_pic.
                    case "video" when !IsAttachedPicture(stream):
                        hasVideo = true;
                        break;
                }
            }
        }

        string? formatName = null;
        double? duration = null;

        if (root.TryGetProperty("format", out var format))
        {
            formatName = GetString(format, "format_name");
            // Assente o "N/A" per le registrazioni WebM del browser, che non hanno la durata nell'header.
            if (double.TryParse(GetString(format, "duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                duration = value;
            }
        }

        return new AudioProbe(formatName ?? string.Empty, audioStreams, hasVideo, duration);
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool IsAttachedPicture(JsonElement stream) =>
        stream.TryGetProperty("disposition", out var disposition)
        && disposition.TryGetProperty("attached_pic", out var attached)
        && attached.ValueKind == JsonValueKind.Number
        && attached.GetInt32() == 1;
}
