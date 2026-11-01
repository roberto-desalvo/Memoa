using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Flusso testo: salva il testo scritto o caricato, con titolo e data della nota, e scrive il RecordUid
/// sulla coda di indicizzazione. Il testo resta nel database, mai su Blob Storage né nella coda.
/// </summary>
public sealed class CreateTextRecordFunction(
    MemoaDbContext db,
    CurrentUser currentUser,
    ILogger<CreateTextRecordFunction> logger)
{
    [Function("CreateTextRecord")]
    public async Task<HttpWithIndexingOutput> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "records/text")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<CreateTextRecordRequest>(cancellationToken);

        if (body is null || !RecordInput.IsValidText(body.Text))
        {
            return new HttpWithIndexingOutput { Result = RecordInput.InvalidText() };
        }

        if (RecordInput.ValidateNote(body.Title, body.OccurredAt, out var title, out var occurredAt) is { } invalid)
        {
            return new HttpWithIndexingOutput { Result = invalid };
        }

        var entraObjectId = currentUser.RequiredEntraObjectId;

        // Creato da UserProvisioningMiddleware prima di questa funzione.
        var userId = await db.Users
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => u.Id)
            .SingleAsync(cancellationToken);

        var record = new Record
        {
            RecordUid = Guid.CreateVersion7(),
            UserId = userId,
            Title = title,
            SourceType = SourceType.Text,
            Status = RecordStatus.Processing,
            TagsPendingSince = DateTime.UtcNow,
            Text = body.Text,
            OccurredAt = occurredAt,
        };
        db.Records.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Record di testo {RecordUid} creato e accodato per l'indicizzazione", record.RecordUid);

        // Il messaggio parte dopo il salvataggio: se la scrittura sulla coda fallisce il record resta
        // "in elaborazione" e il client riceve un errore.
        return new HttpWithIndexingOutput
        {
            Result = new ObjectResult(RecordResponses.ToDetail(record)) { StatusCode = StatusCodes.Status201Created },
            IndexingMessage = record.RecordUid.ToString("D"),
        };
    }
}

/// <param name="OccurredAt">Data e ora della nota, con fuso orario: viene salvata in UTC.</param>
public sealed record CreateTextRecordRequest(string? Title, DateTimeOffset? OccurredAt, string? Text);
