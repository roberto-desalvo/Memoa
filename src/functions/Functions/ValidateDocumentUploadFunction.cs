using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Storage.Blobs;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Documents;
using Memoa.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Functions;

/// <summary>
/// Secondo passo del flusso dei file di testo, attivato dall'evento "blob creato" sul contenitore dei file
/// di testo, come <see cref="ValidateUploadFunction"/>: controlla la dimensione, collega il file come
/// allegato al record creato da <see cref="CreateDocumentUploadFunction"/> e scrive l'URL del blob sulla
/// coda di estrazione del testo. Se il file non è valido porta il record a "in errore" e non accoda nulla;
/// il file resta, per poterlo analizzare.
/// Idempotente: Event Grid consegna almeno una volta, anche in parallelo.
/// </summary>
public sealed class ValidateDocumentUploadFunction(
    MemoaDbContext db,
    IOptions<ContainerOptions> containers,
    ILogger<ValidateDocumentUploadFunction> logger)
{
    // Lunghezza della colonna Attachments.OriginalContentType.
    private const int MaxContentTypeLength = 100;

    /// <summary>
    /// Restituisce il messaggio per la coda di estrazione del testo (l'URL del blob, senza SAS), oppure
    /// null se non c'è nulla da accodare.
    /// </summary>
    [Function("ValidateDocumentUpload")]
    [QueueOutput("%Queues:TextExtraction%", Connection = "DataStorage")]
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
        if (blobUri.BlobContainerName != containers.Value.Documents)
        {
            logger.LogError("Evento {EventId} relativo a un contenitore inatteso: ignorato", eventGridEvent.Id);
            return null;
        }

        // Il SAS permette di creare solo i percorsi scelti da CreateDocumentUpload. Il nome del blob non va
        // nei log perché in questo caso non è sotto il nostro controllo.
        if (!RecordBlobService.TryParseUploadBlobName(blobUri.BlobName, out var entraObjectId, out var recordUid))
        {
            logger.LogWarning("Blob con percorso non valido nell'evento {EventId}: ignorato", eventGridEvent.Id);
            return null;
        }

        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Include(r => r.Attachments.Where(a => a.Kind == AttachmentKind.Document))
            .Where(r => r.RecordUid == recordUid && r.User.EntraObjectId == entraObjectId)
            .SingleOrDefaultAsync(cancellationToken);

        // CreateDocumentUpload salva il record prima di restituire il SAS: se manca è stato eliminato
        // dall'utente (o il percorso non corrisponde all'utente del record). Una nota scritta non ha upload.
        if (record is null || record.SourceType != SourceType.Text)
        {
            logger.LogWarning("Nessun record di testo {RecordUid} per l'utente del blob: evento ignorato", recordUid);
            return null;
        }

        // L'allegato esiste già se una consegna precedente ha validato il file.
        if (record.Attachments.Count == 0
            && !await TryAttachAsync(record, blobUri.BlobName, data, Validate(data), cancellationToken))
        {
            return null;
        }

        if (record.Status == RecordStatus.Error)
        {
            logger.LogWarning("Upload del documento {RecordUid} non valido ({ErrorCode})", recordUid, record.ErrorCode);
            return null;
        }

        // Alla seconda consegna si accoda di nuovo solo se il testo non è ancora stato estratto: la prima può
        // aver salvato l'allegato senza riuscire a scrivere sulla coda.
        var enqueue = record.Status == RecordStatus.Processing && record.OriginalTranscript is null;

        if (enqueue)
        {
            logger.LogInformation("Record {RecordUid} accodato per l'estrazione del testo", recordUid);
        }

        return enqueue ? blobUri.ToUri().ToString() : null;
    }

    /// <summary>
    /// Controlli possibili senza leggere il file, con il limite del tipo dichiarato (il più ampio se il
    /// tipo non è ammesso). Il formato reale lo verifica l'estrazione, con il limite di quel formato.
    /// </summary>
    private static string? Validate(StorageBlobCreatedEventData data) => data.ContentLength switch
    {
        null or <= 0 => ValidateUploadFunction.FileEmpty,
        var size when size > (DocumentLimits.MaxSizeBytes(data.ContentType) ?? DocumentLimits.MaxPdfBytes)
            => ValidateUploadFunction.FileTooLarge,
        _ => null,
    };

    /// <summary>
    /// Collega il file al record e, se non è valido, porta il record a "in errore". Restituisce false se
    /// una consegna concorrente ha già modificato il record.
    /// </summary>
    private async Task<bool> TryAttachAsync(
        Record record,
        string blobName,
        StorageBlobCreatedEventData data,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        var contentType = data.ContentType ?? string.Empty;

        // Anche il file non valido resta collegato al record.
        record.Attachments.Add(new Attachment
        {
            Kind = AttachmentKind.Document,
            // Relativo al contenitore dei file di testo.
            OriginalBlobPath = blobName,
            OriginalContentType = contentType.Length > MaxContentTypeLength
                ? contentType[..MaxContentTypeLength]
                : contentType,
            OriginalSizeBytes = data.ContentLength ?? 0,
        });

        if (errorCode is not null)
        {
            record.Status = RecordStatus.Error;
            record.ErrorCode = errorCode;
        }

        // Come in ValidateUpload: UpdatedAt fa verificare RowVersion, quindi tra due consegne concorrenti
        // solo una aggiunge l'allegato.
        db.Entry(record).Property(r => r.UpdatedAt).IsModified = true;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("Record {RecordUid} già validato da una consegna concorrente", record.RecordUid);
            return false;
        }
    }
}
