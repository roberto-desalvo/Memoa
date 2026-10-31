using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Storage.Blobs;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Functions;

/// <summary>
/// Secondo passo del flusso audio, attivato dall'evento "blob creato" sul contenitore degli originali:
/// valida il file, crea il record "in elaborazione" con l'allegato e scrive l'URL del blob sulla coda
/// di normalizzazione. Se il file non è valido crea il record "in errore" e non accoda nulla; il file
/// non viene eliminato, per poterlo analizzare.
/// Idempotente: Event Grid consegna almeno una volta, anche in parallelo.
/// </summary>
public sealed class ValidateUploadFunction(
    MemoaDbContext db,
    IOptions<ContainerOptions> containers,
    ILogger<ValidateUploadFunction> logger)
{
    // Violazione di chiave primaria o di indice univoco.
    private static readonly int[] DuplicateKeyErrors = [2627, 2601];

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

        var userId = await db.Users
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => (int?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);

        // L'utente viene creato alla prima richiesta autenticata, quindi prima di CreateAudioUpload: se
        // manca, l'eccezione fa ripetere la consegna a Event Grid.
        if (userId is null)
        {
            throw new InvalidOperationException($"Nessun utente per il record {recordUid}.");
        }

        var errorCode = Validate(data);

        var existing = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(r => r.RecordUid == recordUid)
            .Select(r => new
            {
                r.UserId,
                r.Status,
                IsNormalized = r.Attachments.Any(a => a.NormalizedBlobPath != null),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null && existing.UserId != userId)
        {
            logger.LogError("Il record {RecordUid} appartiene a un altro utente: evento ignorato", recordUid);
            return null;
        }

        var inserted = existing is null
            && await TryInsertRecordAsync(recordUid, userId.Value, blobUri.BlobName, data, errorCode,
                eventGridEvent.EventTime.UtcDateTime, cancellationToken);

        if (errorCode is not null)
        {
            logger.LogWarning("Upload del record {RecordUid} non valido ({ErrorCode})", recordUid, errorCode);
            return null;
        }

        // Alla seconda consegna si accoda di nuovo solo se l'elaborazione non è andata avanti: la prima
        // può aver salvato il record senza riuscire a scrivere sulla coda. Se l'inserimento è fallito per
        // una consegna concorrente, accoda quella.
        var enqueue = inserted
            || existing is { Status: RecordStatus.Processing, IsNormalized: false };

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
        null or <= 0 => "file_empty",
        > CreateAudioUploadFunction.MaxSizeBytes => "file_too_large",
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

    /// <summary>Restituisce false se il record è già stato inserito da una consegna concorrente.</summary>
    private async Task<bool> TryInsertRecordAsync(
        Guid recordUid,
        int userId,
        string blobName,
        StorageBlobCreatedEventData data,
        string? errorCode,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        var contentType = data.ContentType ?? string.Empty;
        var record = new Record
        {
            RecordUid = recordUid,
            UserId = userId,
            SourceType = SourceType.Audio,
            Status = errorCode is null ? RecordStatus.Processing : RecordStatus.Error,
            ErrorCode = errorCode,
            // Per ora l'unica data disponibile è quella di fine upload.
            OccurredAt = occurredAt,
            // Anche il file non valido resta collegato al record.
            Attachments =
            [
                new Attachment
                {
                    Kind = AttachmentKind.Audio,
                    // Relativo al contenitore degli originali.
                    OriginalBlobPath = blobName,
                    OriginalContentType = contentType.Length > MaxContentTypeLength
                        ? contentType[..MaxContentTypeLength]
                        : contentType,
                    OriginalSizeBytes = data.ContentLength ?? 0,
                },
            ],
        };

        db.Records.Add(record);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && DuplicateKeyErrors.Contains(sql.Number))
        {
            logger.LogInformation("Record {RecordUid} già inserito da una consegna concorrente", recordUid);
            return false;
        }
    }
}
