using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Transcription;

/// <summary>
/// Fast transcription di Azure AI Speech: chiamata sincrona, con l'audio elaborato solo in memoria nella
/// regione della risorsa. Le lingue candidate sono quelle supportate: il servizio riconosce quella parlata
/// frase per frase, e vale quella con più parlato.
/// Chiamata REST diretta, senza SDK; nessun retry automatico, li gestisce la coda.
/// </summary>
public sealed class AzureSpeechTranscriber(HttpClient http, IOptions<SpeechOptions> options) : ITranscriber
{
    private const string ApiVersion = "2024-11-15";

    private static readonly string[] Scopes = ["https://cognitiveservices.azure.com/.default"];

    // Condivisa tra le istanze del client tipizzato, così il token resta in cache.
    private static readonly TokenCredential Credential = new DefaultAzureCredential();

    /// <summary>Locale candidati, con il codice di Users.PreferredLanguage corrispondente.</summary>
    private static readonly Dictionary<string, string> Locales = new(StringComparer.OrdinalIgnoreCase)
    {
        ["it-IT"] = "it",
        ["en-US"] = "en",
    };

    private static readonly string Definition = JsonSerializer.Serialize(new
    {
        locales = Locales.Keys,
        // Il predefinito maschera le parole volgari: in un diario il testo va lasciato com'è.
        profanityFilterMode = "None",
    }, JsonSerializerOptions.Web);

    public string Name => $"azure-speech/fast-transcription-{ApiVersion}";

    public async Task<TranscriptionResult> TranscribeAsync(BinaryData audio, string fallbackLanguage, CancellationToken cancellationToken)
    {
        var token = await Credential.GetTokenAsync(new TokenRequestContext(Scopes), cancellationToken);

        var audioContent = new StreamContent(audio.ToStream());
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/ogg");

        using var content = new MultipartFormDataContent
        {
            { audioContent, "audio", "audio.opus" },
            { new StringContent(Definition, new MediaTypeHeaderValue("application/json")), "definition" },
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(options.Value.Endpoint!, $"speechtotext/transcriptions:transcribe?api-version={ApiVersion}"))
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Nel messaggio solo lo stato e il codice d'errore del servizio: finisce nei log.
            var code = await ReadErrorCodeAsync(response, cancellationToken);
            throw new HttpRequestException(
                $"Azure AI Speech ha risposto {(int)response.StatusCode} ({code ?? "nessun codice"}).",
                null,
                response.StatusCode);
        }

        var body = await response.Content.ReadFromJsonAsync<FastTranscriptionResponse>(JsonSerializerOptions.Web, cancellationToken)
            ?? throw new InvalidOperationException("Risposta vuota di Azure AI Speech.");

        // Audio mono: una sola frase combinata con il testo completo.
        var text = string.Join(' ', (body.CombinedPhrases ?? [])
            .Select(p => p.Text?.Trim())
            .Where(t => !string.IsNullOrEmpty(t)));

        return new TranscriptionResult(text, DetectLanguage(body.Phrases) ?? fallbackLanguage);
    }

    /// <summary>Lingua con più parlato tra le frasi riconosciute; null se nessuna frase ha un locale candidato.</summary>
    private static string? DetectLanguage(IReadOnlyList<Phrase>? phrases) =>
        (phrases ?? [])
            .Where(p => p.Locale is not null && Locales.ContainsKey(p.Locale))
            .GroupBy(p => Locales[p.Locale!])
            .OrderByDescending(g => g.Sum(p => p.DurationMilliseconds))
            .Select(g => g.Key)
            .FirstOrDefault();

    /// <summary>Codice d'errore della risposta ("code" o "error.code"), se il corpo è un JSON che lo contiene.</summary>
    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = json.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                root = error;
            }

            return root.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record FastTranscriptionResponse(IReadOnlyList<CombinedPhrase>? CombinedPhrases, IReadOnlyList<Phrase>? Phrases);

    private sealed record CombinedPhrase(string? Text);

    private sealed record Phrase(long DurationMilliseconds, string? Locale);
}
