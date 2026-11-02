using System.Text;
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
/// URL con SAS di sola lettura del file di testo originale (PDF, Markdown, testo semplice), per scaricarlo
/// dalla pagina del record con il titolo della nota come nome. Viene firmato solo dopo aver verificato che
/// il record appartenga all'utente. Gli originali audio non si scaricano: stanno nel tier Cold.
/// </summary>
public sealed class GetRecordOriginalFunction(
    MemoaDbContext db,
    BlobSasService sasService,
    ILogger<GetRecordOriginalFunction> logger)
{
    // Lunghezza massima del nome senza estensione: il titolo può arrivare a 200 caratteri.
    private const int MaxFileNameLength = 100;

    [Function("GetRecordOriginal")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "records/{recordUid:guid}/original")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        // Il filtro globale sugli allegati limita la query ai record dell'utente autenticato.
        var original = await db.Attachments
            .Where(a => a.Record.RecordUid == recordUid && a.Kind == AttachmentKind.Document)
            .Select(a => new { a.OriginalBlobPath, a.OriginalContentType, a.Record.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (original is null)
        {
            return new NotFoundResult();
        }

        // Il tipo salvato è quello dichiarato dal client: si restituisce solo uno di quelli ammessi, così il
        // browser non interpreta il file come HTML o script.
        var contentType = DocumentLimits.MaxSizeBytes(original.OriginalContentType) is null
            ? "application/octet-stream"
            : DocumentLimits.MediaType(original.OriginalContentType);
        var fileName = FileName(original.Title) + DocumentLimits.Extension(original.OriginalContentType);

        var (url, expiresOn) = await sasService.CreateDocumentDownloadUriAsync(
            original.OriginalBlobPath, fileName, contentType, cancellationToken);

        // Mai l'URL nei log: contiene il SAS.
        logger.LogInformation("Download dell'originale autorizzato per il record {RecordUid}", recordUid);

        return new OkObjectResult(new RecordOriginalResponse(url, expiresOn));
    }

    /// <summary>Il titolo senza i caratteri non ammessi nei nomi di file (Windows è il più restrittivo).</summary>
    private static string FileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*']).ToHashSet();
        var name = new StringBuilder();
        foreach (var c in title)
        {
            name.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
        }

        var result = name.ToString().Trim().TrimEnd('.');
        if (result.Length > MaxFileNameLength)
        {
            result = result[..MaxFileNameLength];
        }

        return result.Length == 0 ? "nota" : result;
    }
}

public sealed record RecordOriginalResponse(Uri Url, DateTimeOffset ExpiresOn);
