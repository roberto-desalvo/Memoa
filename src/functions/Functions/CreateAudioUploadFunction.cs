using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Primo passo del flusso audio: riceve titolo e data della nota, crea il record "in elaborazione",
/// sceglie il percorso del blob e restituisce un URL con SAS valido solo per creare quel file. Il client
/// carica poi l'audio direttamente su Blob Storage; l'evento "blob creato" attiva la validazione, che
/// collega il file al record.
/// </summary>
public sealed class CreateAudioUploadFunction(
    UploadSasService sasService,
    MemoaDbContext db,
    CurrentUser currentUser,
    ILogger<CreateAudioUploadFunction> logger)
{
    /// <summary>Dimensione massima del file caricato, prima della normalizzazione.</summary>
    public const long MaxSizeBytes = 350L * 1024 * 1024;

    [Function("CreateAudioUpload")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "uploads/audio")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        CreateAudioUploadRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<CreateAudioUploadRequest>(cancellationToken);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            body = null;
        }

        // La dimensione dichiarata serve solo a rifiutare subito i file troppo grandi: il SAS non può
        // limitarla, quindi la Function di validazione ricontrolla quella reale.
        if (body is not { SizeBytes: > 0 and <= MaxSizeBytes })
        {
            return new BadRequestObjectResult(new { error = "invalid_size", maxSizeBytes = MaxSizeBytes });
        }

        // Il client manda sempre un titolo: se l'utente non lo specifica usa la data della nota.
        var title = body.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > Record.MaxTitleLength)
        {
            return new BadRequestObjectResult(new { error = "invalid_title", maxLength = Record.MaxTitleLength });
        }

        if (body.OccurredAt is not { } occurredAt)
        {
            return new BadRequestObjectResult(new { error = "invalid_occurred_at" });
        }

        var entraObjectId = currentUser.EntraObjectId
            ?? throw new InvalidOperationException("Utente non autenticato.");

        // Creato da UserProvisioningMiddleware prima di questa funzione.
        var userId = await db.Users
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => u.Id)
            .SingleAsync(cancellationToken);

        var recordUid = Guid.CreateVersion7();

        // Prefisso per utente: la cancellazione GDPR elimina tutti i suoi file con un solo prefisso.
        // Nessuna estensione: il formato reale viene rilevato con ffprobe durante la normalizzazione.
        var blobName = $"{entraObjectId}/{recordUid}";
        var (uploadUrl, expiresOn) = await sasService.CreateUploadUriAsync(blobName, cancellationToken);

        // Il record si salva dopo aver firmato il SAS, così un errore di firma non lascia record orfani.
        // L'allegato lo crea la validazione, quando il file esiste. Se l'upload non si completa il record
        // resta "in elaborazione" (pulizia da definire).
        db.Records.Add(new Record
        {
            RecordUid = recordUid,
            UserId = userId,
            Title = title,
            SourceType = SourceType.Audio,
            Status = RecordStatus.Processing,
            OccurredAt = occurredAt.UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);

        // Mai l'URL nei log: contiene il SAS.
        logger.LogInformation("Upload audio autorizzato per il record {RecordUid}", recordUid);

        return new OkObjectResult(new CreateAudioUploadResponse(recordUid, uploadUrl, expiresOn));
    }
}

/// <param name="OccurredAt">Data e ora della nota, con fuso orario: viene salvata in UTC.</param>
public sealed record CreateAudioUploadRequest(long SizeBytes, string? Title, DateTimeOffset? OccurredAt);

public sealed record CreateAudioUploadResponse(Guid RecordUid, Uri UploadUrl, DateTimeOffset ExpiresOn);
