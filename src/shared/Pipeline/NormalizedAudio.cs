namespace Memoa.Shared.Pipeline;

/// <summary>
/// Contratto tra il job di normalizzazione, che scrive nel contenitore dell'audio normalizzato, e la
/// Function attivata dall'evento "blob creato" su quel contenitore. Il job scrive uno solo dei due blob:
/// <list type="bullet">
/// <item>"&lt;oid&gt;/&lt;RecordUid&gt;.opus": copia Ogg Opus, con la durata nei metadati;</item>
/// <item>"&lt;oid&gt;/&lt;RecordUid&gt;.error.json": esito di un file scartato, con il codice d'errore nei metadati.</item>
/// </list>
/// </summary>
public static class NormalizedAudio
{
    public const string AudioExtension = ".opus";
    public const string ErrorExtension = ".error.json";

    // Chiavi dei metadati dei blob (solo identificativi e valori tecnici).
    public const string DurationMetadata = "durationseconds";
    public const string SourceFormatMetadata = "sourceformat";
    public const string ErrorCodeMetadata = "errorcode";

    /// <summary>Esito usato quando il codice d'errore manca o non è valido.</summary>
    public const string ProcessingFailed = "processing_failed";

    public static string AudioBlobName(Guid entraObjectId, Guid recordUid) =>
        $"{entraObjectId:D}/{recordUid:D}{AudioExtension}";

    public static string ErrorBlobName(Guid entraObjectId, Guid recordUid) =>
        $"{entraObjectId:D}/{recordUid:D}{ErrorExtension}";

    /// <summary>Riconosce i due nomi scritti dal job; false per qualsiasi altro blob.</summary>
    public static bool TryParse(string blobName, out Guid entraObjectId, out Guid recordUid, out bool isError)
    {
        entraObjectId = recordUid = Guid.Empty;
        isError = blobName.EndsWith(ErrorExtension, StringComparison.Ordinal);

        var extension = isError ? ErrorExtension : AudioExtension;
        if (!blobName.EndsWith(extension, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = blobName[..^extension.Length].Split('/');

        return parts.Length == 2
            && Guid.TryParseExact(parts[0], "D", out entraObjectId)
            && Guid.TryParseExact(parts[1], "D", out recordUid);
    }
}
