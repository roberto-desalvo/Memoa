using System.Globalization;
using System.Text.RegularExpressions;
using Azure;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Storage.Blobs;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Storage;
using Memoa.Shared.Pipeline;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Functions;

/// <summary>
/// Quinto passo del flusso audio, attivato dall'evento "blob creato" sul contenitore dell'audio
/// normalizzato, dove il job di normalizzazione scrive uno dei due blob di <see cref="NormalizedAudio"/>:
/// <list type="bullet">
/// <item>copia Opus: salva percorso e durata nell'allegato e scrive l'URL del blob sulla coda di trascrizione;</item>
/// <item>esito di errore: porta il record a "in errore" con il codice scritto dal job.</item>
/// </list>
/// Idempotente: Event Grid consegna almeno una volta, anche in parallelo.
/// </summary>
public sealed partial class EnqueueTranscriptionFunction(
    MemoaDbContext db,
    BlobServiceClient blobService,
    IOptions<ContainerOptions> containers,
    ILogger<EnqueueTranscriptionFunction> logger)
{
    /// <summary>
    /// Restituisce il messaggio per la coda di trascrizione (l'URL della copia Opus, senza SAS), oppure
    /// null se non c'è nulla da accodare.
    /// </summary>
    [Function("EnqueueTranscription")]
    [QueueOutput("%Queues:Transcription%", Connection = "DataStorage")]
    public async Task<string?> Run([EventGridTrigger] EventGridEvent eventGridEvent, CancellationToken cancellationToken)
    {
        if (!eventGridEvent.TryGetSystemEventData(out var systemData)
            || systemData is not StorageBlobCreatedEventData data)
        {
            logger.LogWarning("Evento {EventId} di tipo {EventType} ignorato", eventGridEvent.Id, eventGridEvent.EventType);
            return null;
        }

        var blobUri = new BlobUriBuilder(new Uri(data.Url));

        // La sottoscrizione filtra già sul contenitore: un altro contenitore indica un errore di configurazione.
        if (blobUri.BlobContainerName != containers.Value.Audio)
        {
            logger.LogError("Evento {EventId} relativo a un contenitore inatteso: ignorato", eventGridEvent.Id);
            return null;
        }

        // Solo il job di normalizzazione scrive in questo contenitore: un altro nome indica un errore.
        if (!NormalizedAudio.TryParse(blobUri.BlobName, out var entraObjectId, out var recordUid, out var isError))
        {
            logger.LogWarning("Blob con nome non valido nell'evento {EventId}: ignorato", eventGridEvent.Id);
            return null;
        }

        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Include(r => r.Attachments.Where(a => a.Kind == AttachmentKind.Audio))
            .Where(r => r.RecordUid == recordUid && r.User.EntraObjectId == entraObjectId)
            .SingleOrDefaultAsync(cancellationToken);

        // ValidateUpload crea il record prima di accodare la normalizzazione: se manca è stato eliminato
        // dall'utente (o il percorso non corrisponde all'utente del record).
        if (record is null)
        {
            logger.LogWarning("Nessun record {RecordUid} per l'utente del blob: evento ignorato", recordUid);
            return null;
        }

        var attachment = record.Attachments.SingleOrDefault();
        if (attachment is null)
        {
            logger.LogError("Record {RecordUid} senza allegato audio: evento ignorato", recordUid);
            return null;
        }

        // Errore o record già pronto: un evento duplicato o tardivo non cambia lo stato.
        if (record.Status != RecordStatus.Processing)
        {
            logger.LogInformation("Record {RecordUid} non più in elaborazione: evento ignorato", recordUid);
            return null;
        }

        var metadata = await GetMetadataAsync(blobUri, cancellationToken);
        if (metadata is null)
        {
            logger.LogWarning("Blob del record {RecordUid} non più presente: evento ignorato", recordUid);
            return null;
        }

        return isError
            ? await SaveErrorAsync(record, attachment, metadata, cancellationToken)
            : await SaveNormalizedAsync(record, attachment, blobUri, metadata, cancellationToken);
    }

    private async Task<string?> SaveNormalizedAsync(
        Record record,
        Attachment attachment,
        BlobUriBuilder blobUri,
        IDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        if (attachment.NormalizedBlobPath is null)
        {
            // Relativo al contenitore dell'audio normalizzato, come OriginalBlobPath per gli originali.
            attachment.NormalizedBlobPath = blobUri.BlobName;
            attachment.DurationSeconds = ParseDuration(metadata);

            if (attachment.DurationSeconds is null)
            {
                logger.LogWarning("Record {RecordUid}: durata mancante nei metadati della copia normalizzata", record.RecordUid);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        // Si accoda anche alle consegne successive finché il record è in elaborazione: la prima può aver
        // salvato l'allegato senza riuscire a scrivere sulla coda. Il job di trascrizione è idempotente.
        logger.LogInformation("Record {RecordUid} accodato per la trascrizione", record.RecordUid);
        return blobUri.ToUri().ToString();
    }

    private async Task<string?> SaveErrorAsync(
        Record record,
        Attachment attachment,
        IDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        // Se esiste già la copia normalizzata l'esito di errore viene da un'esecuzione concorrente del
        // job: vale la copia.
        if (attachment.NormalizedBlobPath is not null)
        {
            logger.LogWarning("Record {RecordUid} già normalizzato: esito di errore ignorato", record.RecordUid);
            return null;
        }

        var errorCode = metadata.TryGetValue(NormalizedAudio.ErrorCodeMetadata, out var value) && ErrorCodePattern().IsMatch(value)
            ? value
            : NormalizedAudio.ProcessingFailed;

        record.Status = RecordStatus.Error;
        record.ErrorCode = errorCode;

        // Una modifica concorrente del record (RowVersion) solleva un'eccezione: Event Grid riprova.
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Normalizzazione del record {RecordUid} non riuscita ({ErrorCode})", record.RecordUid, errorCode);
        return null;
    }

    /// <summary>
    /// L'evento non riporta i metadati del blob: si leggono con l'identità gestita. Null se il blob non
    /// esiste più.
    /// </summary>
    private async Task<IDictionary<string, string>?> GetMetadataAsync(BlobUriBuilder blobUri, CancellationToken cancellationToken)
    {
        var blob = blobService
            .GetBlobContainerClient(blobUri.BlobContainerName)
            .GetBlobClient(blobUri.BlobName);

        try
        {
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.Metadata;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static int? ParseDuration(IDictionary<string, string> metadata) =>
        metadata.TryGetValue(NormalizedAudio.DurationMetadata, out var value)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds > 0
                ? seconds
                : null;

    /// <summary>Codice d'errore del job: minuscole e trattini bassi, entro la lunghezza di Records.ErrorCode.</summary>
    [GeneratedRegex("^[a-z_]{1,50}$")]
    private static partial Regex ErrorCodePattern();
}
