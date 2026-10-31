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
/// URL con SAS di sola lettura della copia normalizzata, per riprodurla nella pagina del record. Viene
/// firmato solo dopo aver verificato che il record appartenga all'utente.
/// </summary>
public sealed class GetRecordAudioFunction(
    MemoaDbContext db,
    BlobSasService sasService,
    ILogger<GetRecordAudioFunction> logger)
{
    [Function("GetRecordAudio")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "records/{recordUid:guid}/audio")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        // Il filtro globale sugli allegati limita la query ai record dell'utente autenticato.
        var blobPath = await db.Attachments
            .Where(a => a.Record.RecordUid == recordUid && a.Kind == AttachmentKind.Audio)
            .Select(a => a.NormalizedBlobPath)
            .FirstOrDefaultAsync(cancellationToken);

        if (blobPath is null)
        {
            return new NotFoundResult();
        }

        var (url, expiresOn) = await sasService.CreateAudioReadUriAsync(blobPath, cancellationToken);

        // Mai l'URL nei log: contiene il SAS.
        logger.LogInformation("Lettura dell'audio autorizzata per il record {RecordUid}", recordUid);

        return new OkObjectResult(new RecordAudioResponse(url, expiresOn));
    }
}

public sealed record RecordAudioResponse(Uri Url, DateTimeOffset ExpiresOn);
