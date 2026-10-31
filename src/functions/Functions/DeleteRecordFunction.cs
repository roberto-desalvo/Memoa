using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Eliminazione di un record da parte dell'utente: tutti i suoi blob, originale compreso, poi le righe
/// (allegati e chunk a cascata). I messaggi già in coda non si possono togliere: gli handler ignorano i
/// record che non esistono più.
/// </summary>
public sealed class DeleteRecordFunction(
    MemoaDbContext db,
    CurrentUser currentUser,
    RecordBlobService blobs,
    ILogger<DeleteRecordFunction> logger)
{
    [Function("DeleteRecord")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "records/{recordUid:guid}")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        // Il filtro globale restituisce solo i record dell'utente autenticato.
        if (!await db.Records.AnyAsync(r => r.RecordUid == recordUid, cancellationToken))
        {
            return new NotFoundResult();
        }

        // Prima i blob: se il database fallisce il record resta e l'utente può riprovare, mentre il
        // contrario lascerebbe file senza record.
        await blobs.DeleteAllAsync(currentUser.RequiredEntraObjectId, recordUid, cancellationToken);

        await db.Records
            .Where(r => r.RecordUid == recordUid)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Record {RecordUid} eliminato", recordUid);
        return new NoContentResult();
    }
}
