using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Storage.Blobs;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Functions;

/// <summary>
/// Secondo passo del flusso audio, attivato dall'evento "blob creato" sul contenitore degli originali:
/// valida il file, lo collega come allegato al record creato da CreateAudioUpload e scrive l'URL del blob
/// sulla coda di normalizzazione. Se il file non è valido porta il record a "in errore" e non accoda
/// nulla; il file non viene eliminato, per poterlo analizzare.
/// Idempotente: Event Grid consegna almeno una volta, anche in parallelo.
/// </summary>
public sealed class ValidateUploadFunction(
    MemoaDbContext db,
    IOptions<ContainerOptions> containers,
    ILogger<ValidateUploadFunction> logger)
{
    // Esiti della validazione: dipendono solo dal file, quindi riprovare non serve.
    public const string FileEmpty = "file_empty";
    public const string FileTooLarge = "file_too_large";

    // Lunghezza della colonna Attachments.OriginalContentType.
    private const int MaxContentTypeLength = 100;

    /// <summary>
    /// Restituisce il messaggio per la coda di normalizzazione (l'URL del blob, senza SAS), oppure
    /// null se non c'è nulla da accodare.
    /// </summary>
    [Function("ValidateUpload")]
    [QueueOutput("%Queues:Normalization%", Connection = "DataStorage")]
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
        if (blobUri.BlobContainerName != containers.Value.Uploads)
        {
            logger.LogError("Evento {EventId} relativo a un contenitore inatteso: ignorato", eventGridEvent.Id);
            return null;
        }

        // Il SAS permette di creare solo i percorsi scelti da CreateAudioUpload: qualsiasi altro file è
        // anomalo e non ha un record a cui collegarsi. Il nome del blob non va nei log perché in questo
        // caso non è sotto il nostro controllo.
        if (!TryParseBlobName(blobUri.BlobName, out var entraObjectId, out var recordUid))
        {
            logger.LogWarning("Blob con percorso non valido nell'evento {EventId}: ignorato", eventGridEvent.Id);
            return null;
        }

        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Include(r => r.Attachments.Where(a => a.Kind == AttachmentKind.Audio))
            .Where(r => r.RecordUid == recordUid && r.User.EntraObjectId == entraObjectId)
            .SingleOrDefaultAsync(cancellationToken);

        // CreateAudioUpload salva il record prima di restituire il SAS: se manca è stato eliminato
        // dall'utente (o il percorso non corrisponde all'utente del record).
        if (record is null)
        {
            logger.LogWarning("Nessun record {RecordUid} per l'utente del blob: evento ignorato", recordUid);
            return null;
        }

        var errorCode = Validate(data);

        // L'allegato esiste già se una consegna precedente ha validato il file.
        if (record.Attachments.Count == 0
            && !await TryAttachAsync(record, blobUri.BlobName, data, errorCode, cancellationToken))
        {
            return null;
        }

        if (record.Status == RecordStatus.Error)
        {
            logger.LogWarning("Upload del record {RecordUid} non valido ({ErrorCode})", recordUid, record.ErrorCode);
            return null;
        }

        // Alla seconda consegna si accoda di nuovo solo se l'elaborazione non è andata avanti: la prima
        // può aver salvato l'allegato senza riuscire a scrivere sulla coda.
        var enqueue = record.Status == RecordStatus.Processing
            && record.Attachments.All(a => a.NormalizedBlobPath is null);

        if (enqueue)
        {
            logger.LogInformation("Record {RecordUid} accodato per la normalizzazione", recordUid);
        }

        return enqueue ? blobUri.ToUri().ToString() : null;
    }

    /// <summary>
    /// Controlli possibili senza leggere il file. Il formato reale (non l'estensione né il tipo
    /// dichiarato dal client) e la durata li verifica ffprobe nel job di normalizzazione.
    /// </summary>
    private static string? Validate(StorageBlobCreatedEventData data) => data.ContentLength switch
    {
        null or <= 0 => FileEmpty,
        > CreateAudioUploadFunction.MaxSizeBytes => FileTooLarge,
        _ => null,
    };

    /// <summary>Il percorso ha la forma "&lt;oid utente&gt;/&lt;RecordUid&gt;" (vedi CreateAudioUpload).</summary>
    private static bool TryParseBlobName(string blobName, out Guid entraObjectId, out Guid recordUid)
    {
        entraObjectId = recordUid = Guid.Empty;
        var parts = blobName.Split('/');

        return parts.Length == 2
            && Guid.TryParseExact(parts[0], "D", out entraObjectId)
            && Guid.TryParseExact(parts[1], "D", out recordUid);
    }

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
            Kind = AttachmentKind.Audio,
            // Relativo al contenitore degli originali.
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

        // Aggiungere l'allegato non modifica il record: segnando UpdatedAt (valorizzato al salvataggio) il
        // salvataggio verifica RowVersion, quindi tra due consegne concorrenti solo una aggiunge l'allegato.
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
