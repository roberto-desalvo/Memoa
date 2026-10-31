using Memoa.Functions.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace Memoa.Functions.Functions;

/// <summary>Record per la propria pagina, con il testo.</summary>
public sealed class GetRecordFunction(MemoaDbContext db)
{
    [Function("GetRecord")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "records/{recordUid:guid}")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        // Il filtro globale restituisce solo i record dell'utente autenticato: quelli altrui risultano assenti.
        var record = await db.Records
            .AsNoTracking()
            .Include(r => r.Attachments)
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        return record is null
            ? new NotFoundResult()
            : new OkObjectResult(RecordResponses.ToDetail(record));
    }
}
