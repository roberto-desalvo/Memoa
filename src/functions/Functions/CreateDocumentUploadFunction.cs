using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Documents;
using Memoa.Functions.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Primo passo del flusso dei file di testo (PDF, Markdown, testo semplice), come
/// <see cref="CreateAudioUploadFunction"/>: crea il record di testo "in elaborazione" senza testo e
/// restituisce un URL con SAS valido solo per creare il file nel contenitore dei file di testo. L'evento
/// "blob creato" attiva <see cref="ValidateDocumentUploadFunction"/>; se il file non arriva entro la
/// scadenza del SAS, il record viene eliminato (<see cref="UploadExpiryScheduler"/>).
/// </summary>
public sealed class CreateDocumentUploadFunction(
    BlobSasService sasService,
    UploadExpiryScheduler expiryScheduler,
    MemoaDbContext db,
    CurrentUser currentUser,
    ILogger<CreateDocumentUploadFunction> logger)
{
    [Function("CreateDocumentUpload")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "uploads/document")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<CreateDocumentUploadRequest>(cancellationToken);

        // Tipo e dimensione dichiarati servono solo a rifiutare subito i file non ammessi: la validazione
        // ricontrolla la dimensione reale e l'estrazione il formato reale.
        if (DocumentLimits.MaxSizeBytes(body?.ContentType) is not { } maxSizeBytes)
        {
            return new BadRequestObjectResult(new { error = "invalid_content_type" });
        }

        if (body!.SizeBytes <= 0 || body.SizeBytes > maxSizeBytes)
        {
            return new BadRequestObjectResult(new { error = "invalid_size", maxSizeBytes });
        }

        if (RecordInput.ValidateNote(body.Title, body.OccurredAt, out var title, out var occurredAt) is { } invalid)
        {
            return invalid;
        }

        var entraObjectId = currentUser.RequiredEntraObjectId;

        // Garantito da UserRegistrationMiddleware prima di questa funzione.
        var userId = await db.Users
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => u.Id)
            .SingleAsync(cancellationToken);

        var recordUid = Guid.CreateVersion7();

        var blobName = RecordBlobService.UploadBlobName(entraObjectId, recordUid);
        var (uploadUrl, expiresOn) = await sasService.CreateDocumentUploadUriAsync(blobName, cancellationToken);

        // Stesso ordine di CreateAudioUpload: prima il controllo di scadenza, poi il record.
        await expiryScheduler.ScheduleAsync(recordUid, cancellationToken);

        // Il testo lo scrive l'estrazione; l'allegato lo crea la validazione, quando il file esiste.
        db.Records.Add(new Record
        {
            RecordUid = recordUid,
            UserId = userId,
            Title = title,
            SourceType = SourceType.Text,
            Status = RecordStatus.Processing,
            TagsPendingSince = DateTime.UtcNow,
            OccurredAt = occurredAt,
        });
        await db.SaveChangesAsync(cancellationToken);

        // Mai l'URL nei log: contiene il SAS.
        logger.LogInformation("Upload documento autorizzato per il record {RecordUid}", recordUid);

        return new OkObjectResult(new CreateAudioUploadResponse(recordUid, uploadUrl, expiresOn));
    }
}

/// <param name="ContentType">Tipo del file: application/pdf, text/markdown o text/plain.</param>
/// <param name="OccurredAt">Data e ora della nota, con fuso orario: viene salvata in UTC.</param>
public sealed record CreateDocumentUploadRequest(long SizeBytes, string? ContentType, string? Title, DateTimeOffset? OccurredAt);
