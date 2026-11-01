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
/// Eliminazione dell'account da parte dell'utente: tutti i suoi blob (prefisso "&lt;oid&gt;/" nei tre
/// contenitori), poi le assegnazioni dei tag e l'utente applicativo con record, allegati, chunk e tag a
/// cascata, infine l'account di accesso. I messaggi già in coda non si possono togliere: contengono solo
/// identificativi e URL di blob, e gli handler ignorano i record e i tag che non esistono più.
/// </summary>
public sealed class DeleteAccountFunction(
    MemoaDbContext db,
    CurrentUser currentUser,
    RecordBlobService blobs,
    IIdentityAccountService identityAccounts,
    ILogger<DeleteAccountFunction> logger)
{
    [Function("DeleteAccount")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "me")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var entraObjectId = currentUser.RequiredEntraObjectId;

        // Creato da UserProvisioningMiddleware prima di questa funzione.
        var userId = await db.Users
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => u.Id)
            .SingleAsync(cancellationToken);

        // Prima i blob, come in DeleteRecord: se il database fallisce l'utente resta e può riprovare, mentre
        // il contrario lascerebbe file senza utente. Ogni passo è idempotente.
        await blobs.DeleteUserAsync(entraObjectId, cancellationToken);

        // Nessuna cascata da Tags a RecordTags (vedi RecordTagConfiguration): le assegnazioni vanno eliminate
        // prima, altrimenti la cascata da Users verso Tags potrebbe fallire.
        await db.RecordTags
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(rt => rt.Tag.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await identityAccounts.DeleteAsync(entraObjectId, cancellationToken);

        logger.LogInformation("Account dell'utente {UserId} eliminato", userId);
        return new NoContentResult();
    }
}
