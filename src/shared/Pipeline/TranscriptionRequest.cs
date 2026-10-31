using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Memoa.Shared.Pipeline;

/// <summary>
/// Messaggio della coda di trascrizione, scritto da EnqueueTranscription e letto dal job di trascrizione.
/// Contiene solo l'URL della copia normalizzata (senza SAS) e un codice lingua: mai contenuti.
/// </summary>
/// <param name="BlobUrl">URL del blob "&lt;oid&gt;/&lt;RecordUid&gt;.opus" nel contenitore dell'audio normalizzato.</param>
/// <param name="FallbackLanguage">Lingua dell'account, usata se il rilevamento automatico è incerto.</param>
public sealed record TranscriptionRequest(string BlobUrl, string FallbackLanguage)
{
    /// <summary>Lingue supportate: gli stessi codici di Users.PreferredLanguage.</summary>
    public static readonly IReadOnlyList<string> SupportedLanguages = ["it", "en"];

    public string Serialize() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);

    /// <summary>False se il messaggio non è un JSON valido, manca un campo o la lingua non è supportata.</summary>
    public static bool TryParse(string body, [NotNullWhen(true)] out TranscriptionRequest? request)
    {
        try
        {
            request = JsonSerializer.Deserialize<TranscriptionRequest>(body, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is not { BlobUrl: not null, FallbackLanguage: not null }
            || !SupportedLanguages.Contains(request.FallbackLanguage))
        {
            request = null;
            return false;
        }

        return true;
    }
}
