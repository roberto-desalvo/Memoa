using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoa.Functions.Functions;

/// <summary>
/// Record nella lista della homepage. <paramref name="Excerpt"/> è l'inizio del testo (al massimo
/// <see cref="ListRecordsFunction.ExcerptLength"/> caratteri), null se il record non ha ancora un testo.
/// <paramref name="DurationSeconds"/> è la durata dell'audio, null finché non è stato normalizzato o se il
/// record non ha audio. <paramref name="Tags"/> sono i tag assegnati, senza quelli esclusi dall'utente.
/// </summary>
public sealed record RecordSummary(
    Guid RecordUid,
    string Title,
    DateTimeOffset OccurredAt,
    string SourceType,
    string Status,
    bool CanRetry,
    string? Excerpt,
    int? DurationSeconds,
    IReadOnlyList<RecordTagItem> Tags);

/// <summary>Tag assegnato a un record, in automatico o dall'utente.</summary>
public sealed record RecordTagItem(Guid TagUid, string Name, bool IsAutomatic);

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
    string? ProcessingStep,
    IReadOnlyList<RecordTagItem> Tags);

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

    /// <summary>Carica i tag visibili del record (non quelli esclusi), necessari a <see cref="ToDetail"/>.</summary>
    public static IQueryable<Record> IncludeTags(this IQueryable<Record> records) =>
        records
            .Include(r => r.Tags.Where(rt => rt.Source != RecordTagSource.Excluded))
            .ThenInclude(rt => rt.Tag);

    /// <summary>
    /// Record per la lista della homepage e per quella di un tag, dal più recente per data della nota e, a
    /// parità di data, per titolo. Dei testi solo l'estratto iniziale, tagliato nel database (SUBSTRING).
    /// </summary>
    public static async Task<List<RecordSummary>> ToSummariesAsync(
        this IQueryable<Record> records, CancellationToken cancellationToken)
    {
        var rows = await records
            .AsNoTracking()
            .OrderByDescending(r => r.OccurredAt)
            .ThenBy(r => r.Title)
            .Select(r => new
            {
                r.RecordUid,
                r.Title,
                r.OccurredAt,
                r.SourceType,
                r.Status,
                r.ErrorCode,
                HasText = r.Text != null,
                Excerpt = r.Text != null ? r.Text.Substring(0, ListRecordsFunction.ExcerptLength) : null,
                HasAttachment = r.Attachments.Any(),
                DurationSeconds = r.Attachments
                    .Where(a => a.Kind == AttachmentKind.Audio)
                    .Select(a => a.DurationSeconds)
                    .FirstOrDefault(),
                Tags = r.Tags
                    .Where(rt => rt.Source != RecordTagSource.Excluded)
                    .OrderBy(rt => rt.Tag.Name)
                    .Select(rt => new { rt.Tag.TagUid, rt.Tag.Name, rt.Source })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new RecordSummary(
                r.RecordUid,
                r.Title,
                AsUtc(r.OccurredAt),
                r.SourceType.ToString(),
                r.Status.ToString(),
                CanRetry(r.Status, r.SourceType, r.ErrorCode, r.HasText, r.HasAttachment),
                r.Excerpt,
                r.DurationSeconds,
                r.Tags.Select(t => new RecordTagItem(t.TagUid, t.Name, t.Source == RecordTagSource.Auto)).ToList()))
            .ToList();
    }

    /// <summary>Tag visibili del record caricati con <see cref="IncludeTags"/>, in ordine di nome.</summary>
    public static List<RecordTagItem> TagsOf(Record record) =>
        record.Tags
            .Where(rt => rt.Source != RecordTagSource.Excluded)
            .OrderBy(rt => rt.Tag.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(rt => new RecordTagItem(rt.Tag.TagUid, rt.Tag.Name, rt.Source == RecordTagSource.Auto))
            .ToList();

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
            ProcessingStep(record, audio),
            TagsOf(record));
    }
}
