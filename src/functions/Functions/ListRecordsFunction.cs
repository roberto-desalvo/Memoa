using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace Memoa.Functions.Functions;

/// <summary>
/// Lista dei record dell'utente per la homepage, dal più recente per data della nota e, a parità di data,
/// per titolo. Il frontend la richiede ogni 10 secondi finché ci sono record in elaborazione e l'app è
/// visibile: la risposta contiene solo i campi della lista e, dei testi, solo l'estratto iniziale.
/// </summary>
public sealed class ListRecordsFunction(MemoaDbContext db)
{
    /// <summary>
    /// Lunghezza dell'estratto: basta per le due righe mostrate nella lista anche su schermi larghi, e
    /// tiene leggera la risposta del polling. Il taglio avviene nel database (SUBSTRING).
    /// </summary>
    public const int ExcerptLength = 200;

    [Function("ListRecords")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "records")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        // Il filtro globale limita la query all'utente autenticato.
        var rows = await db.Records
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
                Excerpt = r.Text != null ? r.Text.Substring(0, ExcerptLength) : null,
                HasAttachment = r.Attachments.Any(),
                DurationSeconds = r.Attachments
                    .Where(a => a.Kind == AttachmentKind.Audio)
                    .Select(a => a.DurationSeconds)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var records = rows.Select(r => new RecordSummary(
            r.RecordUid,
            r.Title,
            RecordResponses.AsUtc(r.OccurredAt),
            r.SourceType.ToString(),
            r.Status.ToString(),
            RecordResponses.CanRetry(r.Status, r.SourceType, r.ErrorCode, r.HasText, r.HasAttachment),
            r.Excerpt,
            r.DurationSeconds));

        return new OkObjectResult(records);
    }
}
