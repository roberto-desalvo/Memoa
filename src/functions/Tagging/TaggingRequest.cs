using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Memoa.Functions.Tagging;

/// <summary>
/// Messaggio della coda dei tag, letto da TagRecordsFunction. Indica un record appena indicizzato (da
/// confrontare con tutti i tag attivi dell'utente) oppure un tag creato, modificato o riattivato (da
/// confrontare con tutti i record pronti dell'utente). Solo identificativi, mai nomi o descrizioni.
/// </summary>
public sealed record TaggingRequest(Guid? RecordUid, Guid? TagUid)
{
    public static TaggingRequest ForRecord(Guid recordUid) => new(recordUid, null);

    public static TaggingRequest ForTag(Guid tagUid) => new(null, tagUid);

    public string Serialize() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);

    /// <summary>False se il messaggio non è un JSON valido o non indica esattamente uno tra record e tag.</summary>
    public static bool TryParse(string body, [NotNullWhen(true)] out TaggingRequest? request)
    {
        try
        {
            request = JsonSerializer.Deserialize<TaggingRequest>(body, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || request.RecordUid.HasValue == request.TagUid.HasValue)
        {
            request = null;
            return false;
        }

        return true;
    }
}
