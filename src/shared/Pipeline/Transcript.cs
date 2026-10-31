namespace Memoa.Shared.Pipeline;

/// <summary>
/// Contratto tra il job di trascrizione, che scrive nel contenitore delle trascrizioni, e la Function
/// attivata dall'evento "blob creato" su quel contenitore. Il job scrive uno solo dei due blob:
/// <list type="bullet">
/// <item>"&lt;oid&gt;/&lt;RecordUid&gt;.txt": trascrizione in UTF-8, con lingua e motore nei metadati;</item>
/// <item>"&lt;oid&gt;/&lt;RecordUid&gt;.error.json": esito di una trascrizione non riuscita, con il codice d'errore nei metadati.</item>
/// </list>
/// </summary>
public static class Transcript
{
    public const string TextExtension = ".txt";
    public const string ErrorExtension = ".error.json";

    // Chiavi dei metadati dei blob (solo identificativi e valori tecnici, mai il testo).

    /// <summary>Lingua della trascrizione ("it" o "en"): quella rilevata o, se incerta, quella dell'account.</summary>
    public const string LanguageMetadata = "language";

    /// <summary>Lingua più probabile secondo il rilevamento automatico; assente se il motore non la fornisce.</summary>
    public const string DetectedLanguageMetadata = "detectedlanguage";

    /// <summary>Probabilità della lingua rilevata (0-1, punto decimale); assente se il motore non la fornisce.</summary>
    public const string LanguageProbabilityMetadata = "languageprobability";

    /// <summary>Motore e modello di trascrizione, per valutare la qualità delle trascrizioni.</summary>
    public const string EngineMetadata = "engine";

    public const string ErrorCodeMetadata = "errorcode";

    /// <summary>Nessun parlato riconosciuto: la trascrizione è vuota.</summary>
    public const string NoSpeech = "no_speech";

    /// <summary>Tentativi esauriti.</summary>
    public const string ProcessingFailed = NormalizedAudio.ProcessingFailed;

    public static string TextBlobName(Guid entraObjectId, Guid recordUid) =>
        $"{entraObjectId:D}/{recordUid:D}{TextExtension}";

    public static string ErrorBlobName(Guid entraObjectId, Guid recordUid) =>
        $"{entraObjectId:D}/{recordUid:D}{ErrorExtension}";

    /// <summary>Riconosce i due nomi scritti dal job; false per qualsiasi altro blob.</summary>
    public static bool TryParse(string blobName, out Guid entraObjectId, out Guid recordUid, out bool isError)
    {
        entraObjectId = recordUid = Guid.Empty;
        isError = blobName.EndsWith(ErrorExtension, StringComparison.Ordinal);

        var extension = isError ? ErrorExtension : TextExtension;
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
