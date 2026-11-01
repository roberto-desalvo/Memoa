using Memoa.Functions.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace Memoa.Functions.Functions;

/// <summary>
/// Lista dei record dell'utente per la homepage, dal più recente per data della nota e, a parità di data,
/// per titolo. Il frontend la richiede ogni 10 secondi finché ci sono record in elaborazione e l'app è
/// visibile: la risposta contiene solo i campi della lista (con i tag) e, dei testi, solo l'estratto
/// iniziale.
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
        var records = await db.Records.ToSummariesAsync(cancellationToken);

        return new OkObjectResult(records);
    }
}
