using Memoa.Functions.Authentication;
using Memoa.Functions.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Primo passo del flusso audio: sceglie il percorso del blob e restituisce un URL con SAS valido solo
/// per creare quel file. Il client carica poi l'audio direttamente su Blob Storage; l'evento
/// "blob creato" attiva la validazione.
/// </summary>
public sealed class CreateAudioUploadFunction(UploadSasService sasService, ILogger<CreateAudioUploadFunction> logger)
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

        var userId = req.HttpContext.User.GetUserId();
        var recordId = Guid.CreateVersion7();

        // Prefisso per utente: la cancellazione GDPR elimina tutti i suoi file con un solo prefisso.
        // Nessuna estensione: il formato reale viene rilevato con ffprobe durante la normalizzazione.
        var blobName = $"{userId}/{recordId}";
        var (uploadUrl, expiresOn) = await sasService.CreateUploadUriAsync(blobName, cancellationToken);

        // Mai l'URL nei log: contiene il SAS.
        logger.LogInformation("Upload audio autorizzato per il record {RecordId}", recordId);

        return new OkObjectResult(new CreateAudioUploadResponse(recordId, uploadUrl, expiresOn));
    }
}

public sealed record CreateAudioUploadRequest(long SizeBytes);

public sealed record CreateAudioUploadResponse(Guid RecordId, Uri UploadUrl, DateTimeOffset ExpiresOn);
