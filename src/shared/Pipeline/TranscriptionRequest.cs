using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Memoa.Shared.Pipeline;

/// <summary>
/// Messaggio della coda di trascrizione, scritto da EnqueueTranscription (e dalle Function HTTP di
/// riprova e rigenerazione) e letto da TranscribeRecord, che lo inoltra uguale sulla coda di ripiego del
/// job di trascrizione se Azure AI Speech non risponde o si superano i limiti.
/// Contiene solo l'URL della copia normalizzata (senza SAS) e un codice lingua: mai contenuti.
/// </summary>
/// <param name="BlobUrl">URL del blob "&lt;oid&gt;/&lt;RecordUid&gt;.opus" nel contenitore dell'audio normalizzato.</param>
/// <param name="FallbackLanguage">Lingua dell'account, usata se il rilevamento automatico è incerto.</param>
public sealed record TranscriptionRequest(string BlobUrl, string FallbackLanguage)
{
    /// <summary>Lingue supportate: gli stessi codici di Users.PreferredLanguage.</summary>
    public static readonly IReadOnlyList<string> SupportedLanguages = ["it", "en"];

    public bool HasSupportedLanguage => SupportedLanguages.Contains(FallbackLanguage);

    public string Serialize() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);

    /// <summary>
    /// False se il messaggio non è un JSON valido o manca un campo. La lingua va verificata a parte
    /// (<see cref="HasSupportedLanguage"/>), così il job può scrivere l'esito per il record del messaggio.
    /// </summary>
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

        if (request is not { BlobUrl: not null, FallbackLanguage: not null })
        {
            request = null;
            return false;
        }

        return true;
    }
}
