using Memoa.Functions.Data.Entities;

namespace Memoa.Functions.Functions;

/// <summary>
/// Record nella lista della homepage. <paramref name="Excerpt"/> è l'inizio del testo (al massimo
/// <see cref="ListRecordsFunction.ExcerptLength"/> caratteri), null se il record non ha ancora un testo.
/// </summary>
public sealed record RecordSummary(
    Guid RecordUid,
    string Title,
    DateTimeOffset OccurredAt,
    string SourceType,
    string Status,
    bool CanRetry,
    string? Excerpt);

/// <summary>Record nella propria pagina. L'audio si chiede a parte (SAS di lettura).</summary>
public sealed record RecordDetail(
    Guid RecordUid,
    string Title,
    DateTimeOffset OccurredAt,
    string SourceType,
    string Status,
    string? ErrorCode,
    string? Text,
    string? Language,
    bool HasAudio,
    int? DurationSeconds,
    bool CanRetry,
    bool IsTranscript,
    bool IsTranscriptEdited,
    bool CanRegenerateTranscript,
    string? ProcessingStep);

internal static class RecordResponses
{
    /// <summary>EF Core legge i datetime2 senza fuso: i valori salvati sono sempre UTC.</summary>
    public static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>
    /// Un record in errore si può riprovare se ha un testo (si rifà l'indicizzazione) oppure se è un audio
    /// caricato che ha superato la validazione (si riparte dalla normalizzazione). Un file vuoto o troppo
    /// grande resta tale: riprovare non serve.
    /// </summary>
    public static bool CanRetry(RecordStatus status, SourceType sourceType, string? errorCode, bool hasText, bool hasAttachment) =>
        status == RecordStatus.Error
        && (hasText
            || (sourceType == SourceType.Audio
                && hasAttachment
                && errorCode is not (ValidateUploadFunction.FileEmpty or ValidateUploadFunction.FileTooLarge)));

    /// <summary>
    /// La trascrizione di un audio pronto si può rigenerare dalla copia normalizzata (vedi
    /// <see cref="RegenerateTranscriptFunction"/>).
    /// </summary>
    public static bool CanRegenerateTranscript(RecordStatus status, SourceType sourceType, string? normalizedBlobPath) =>
        status == RecordStatus.Ready && sourceType == SourceType.Audio && normalizedBlobPath is not null;

    /// <summary>
    /// Passo in corso di un record in elaborazione, per mostrarne l'avanzamento; null se non è in elaborazione.
    /// Un audio senza copia normalizzata è da caricare o da normalizzare (anche dopo "riprova", che la
    /// azzera); senza OriginalTranscript è da trascrivere, anche durante una rigenerazione, in cui il testo
    /// resta quello precedente. Negli altri casi manca solo l'indicizzazione.
    /// </summary>
    public static string? ProcessingStep(Record record, Attachment? audio) =>
        record.Status != RecordStatus.Processing ? null
        : record.SourceType != SourceType.Audio ? "Indexing"
        : audio?.NormalizedBlobPath is null ? "Preparing"
        : record.OriginalTranscript is null ? "Transcribing"
        : "Indexing";

    public static RecordDetail ToDetail(Record record)
    {
        var audio = record.Attachments.FirstOrDefault(a => a.Kind == AttachmentKind.Audio);
        var isTranscript = record.SourceType == SourceType.Audio && record.Text is not null;

        return new RecordDetail(
            record.RecordUid,
            record.Title,
            AsUtc(record.OccurredAt),
            record.SourceType.ToString(),
            record.Status.ToString(),
            record.ErrorCode,
            record.Text,
            record.Language,
            audio?.NormalizedBlobPath is not null,
            audio?.DurationSeconds,
            CanRetry(record.Status, record.SourceType, record.ErrorCode, record.Text is not null, audio is not null),
            isTranscript,
            // OriginalTranscript è null solo durante una rigenerazione (o se è fallita): il testo resta quello di prima.
            isTranscript && record.OriginalTranscript is not null && record.Text != record.OriginalTranscript,
            CanRegenerateTranscript(record.Status, record.SourceType, audio?.NormalizedBlobPath),
            ProcessingStep(record, audio));
    }
}
