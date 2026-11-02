namespace Memoa.Functions.Transcription;

/// <summary>
/// Servizio di trascrizione chiamato dalla Function, sostituibile (es. un'altra API cloud al posto di
/// Azure AI Speech). Il ripiego sul job di trascrizione resta fuori dall'interfaccia, nella Function.
/// </summary>
public interface ITranscriber
{
    /// <summary>Servizio e modello, solo per i log.</summary>
    string Name { get; }

    /// <summary>
    /// Trascrive la copia normalizzata (Ogg Opus mono). Il timeout lo decide il chiamante con il token:
    /// l'annullamento si propaga come <see cref="OperationCanceledException"/>. Le risposte di errore del
    /// servizio si segnalano con <see cref="HttpRequestException"/> e il relativo StatusCode.
    /// </summary>
    /// <param name="audio">Contenuto della copia normalizzata.</param>
    /// <param name="fallbackLanguage">Lingua dell'account, da usare se il rilevamento automatico non dà un risultato.</param>
    Task<TranscriptionResult> TranscribeAsync(BinaryData audio, string fallbackLanguage, CancellationToken cancellationToken);
}

/// <param name="Text">Testo trascritto; vuoto se non è stato riconosciuto parlato.</param>
/// <param name="Language">Lingua della trascrizione ("it" o "en").</param>
public sealed record TranscriptionResult(string Text, string Language);
