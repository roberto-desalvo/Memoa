namespace Memoa.Transcriber.Engine;

/// <summary>
/// Motore di trascrizione, sostituibile (es. un'API cloud al posto di Whisper): il job resta una
/// trasformazione da audio a testo indipendente dal motore.
/// </summary>
public interface ITranscriptionEngine
{
    /// <summary>Motore e modello, salvati nei metadati della trascrizione.</summary>
    string Name { get; }

    /// <summary>Trascrive la copia normalizzata (Ogg Opus mono).</summary>
    /// <param name="audioPath">File locale della copia normalizzata.</param>
    /// <param name="fallbackLanguage">Lingua dell'account, da usare se il rilevamento automatico è incerto.</param>
    Task<TranscriptionResult> TranscribeAsync(string audioPath, string fallbackLanguage, CancellationToken cancellationToken);
}

/// <param name="Text">Testo trascritto; vuoto se non è stato riconosciuto parlato.</param>
/// <param name="Language">Lingua usata per la trascrizione.</param>
/// <param name="DetectedLanguage">Esito del rilevamento automatico, se il motore lo fornisce.</param>
/// <param name="LanguageProbability">Probabilità della lingua rilevata, se il motore la fornisce.</param>
public sealed record TranscriptionResult(string Text, string Language, string? DetectedLanguage, float? LanguageProbability);
