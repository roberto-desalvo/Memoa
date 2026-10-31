namespace Memoa.Transcriber.Engine;

/// <summary>
/// Errore di una fase della trascrizione, con il codice di <see cref="Memoa.Shared.Pipeline.Transcript"/>
/// che il job scrive nell'esito se i tentativi sono esauriti. Il messaggio contiene solo dati tecnici:
/// finisce nei log insieme all'eccezione originale.
/// </summary>
public sealed class TranscriptionException(string errorCode, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}
