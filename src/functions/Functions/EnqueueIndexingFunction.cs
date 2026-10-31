using System.Text.RegularExpressions;
using Azure;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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
/// Settimo passo del flusso audio, attivato dall'evento "blob creato" sul contenitore delle trascrizioni,
/// dove il job di trascrizione scrive uno dei due blob di <see cref="Transcript"/>:
/// <list type="bullet">
/// <item>trascrizione: salva testo e lingua nel record e scrive sulla coda di indicizzazione il RecordUid;</item>
/// <item>esito di errore: porta il record a "in errore" con il codice scritto dal job.</item>
/// </list>
/// Idempotente: Event Grid consegna almeno una volta, anche in parallelo.
/// </summary>
public sealed partial class EnqueueIndexingFunction(
    MemoaDbContext db,
    BlobServiceClient blobService,
    IOptions<ContainerOptions> containers,
    ILogger<EnqueueIndexingFunction> logger)
{
    /// <summary>
    /// Restituisce il messaggio per la coda di indicizzazione (il RecordUid in formato "D", lo stesso
    /// messaggio scritto dal flusso testo), oppure null se non c'è nulla da accodare.
    /// </summary>
    [Function("EnqueueIndexing")]
    [QueueOutput("%Queues:Indexing%", Connection = "DataStorage")]
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
        if (blobUri.BlobContainerName != containers.Value.Transcripts)
        {
            logger.LogError("Evento {EventId} relativo a un contenitore inatteso: ignorato", eventGridEvent.Id);
            return null;
        }

        // Solo il job di trascrizione scrive in questo contenitore: un altro nome indica un errore.
        if (!Transcript.TryParse(blobUri.BlobName, out var entraObjectId, out var recordUid, out var isError))
        {
            logger.LogWarning("Blob con nome non valido nell'evento {EventId}: ignorato", eventGridEvent.Id);
            return null;
        }

        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Include(r => r.User)
            .Where(r => r.RecordUid == recordUid && r.User.EntraObjectId == entraObjectId)
            .SingleOrDefaultAsync(cancellationToken);

        // Il record esiste da ValidateUpload: se manca è stato eliminato dall'utente (o il percorso non
        // corrisponde all'utente del record).
        if (record is null)
        {
            logger.LogWarning("Nessun record {RecordUid} per l'utente del blob: evento ignorato", recordUid);
            return null;
        }

        if (record.SourceType != SourceType.Audio)
        {
            logger.LogError("Trascrizione per il record {RecordUid} che non è audio: evento ignorato", recordUid);
            return null;
        }

        // Errore o record già pronto: un evento duplicato o tardivo non cambia lo stato.
        if (record.Status != RecordStatus.Processing)
        {
            logger.LogInformation("Record {RecordUid} non più in elaborazione: evento ignorato", recordUid);
            return null;
        }

        var blob = await DownloadAsync(blobUri, cancellationToken);
        if (blob is null)
        {
            logger.LogWarning("Blob del record {RecordUid} non più presente: evento ignorato", recordUid);
            return null;
        }

        var metadata = blob.Details.Metadata;

        return isError
            ? await SaveErrorAsync(record, metadata, cancellationToken)
            : await SaveTranscriptAsync(record, blob.Content.ToString(), metadata, cancellationToken);
    }

    private async Task<string?> SaveTranscriptAsync(
        Record record,
        string text,
        IDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        // Il job scrive l'esito no_speech al posto di una trascrizione vuota: una vuota è comunque
        // inutilizzabile per l'indicizzazione.
        if (string.IsNullOrWhiteSpace(text))
        {
            logger.LogWarning("Record {RecordUid}: trascrizione vuota", record.RecordUid);
            return await SetErrorAsync(record, Transcript.NoSpeech, cancellationToken);
        }

        if (record.OriginalTranscript is null)
        {
            // OriginalTranscript resta la trascrizione automatica; Text è quella corrente, che l'utente
            // potrà modificare.
            record.OriginalTranscript = text;
            record.Text = text;

            // Il job scrive la lingua rilevata o, se incerta, quella dell'account: se manca o non è
            // supportata si ripiega sulla lingua dell'account.
            if (metadata.TryGetValue(Transcript.LanguageMetadata, out var language)
                && TranscriptionRequest.SupportedLanguages.Contains(language))
            {
                record.Language = language;
            }
            else
            {
                logger.LogWarning("Record {RecordUid}: lingua mancante o non valida nei metadati della trascrizione", record.RecordUid);
                record.Language = record.User.PreferredLanguage;
            }

            // Una modifica concorrente del record (RowVersion) solleva un'eccezione: Event Grid riprova.
            await db.SaveChangesAsync(cancellationToken);
        }

        // Si accoda anche alle consegne successive finché il record è in elaborazione: la prima può aver
        // salvato la trascrizione senza riuscire a scrivere sulla coda. La Function di indicizzazione è idempotente.
        logger.LogInformation("Record {RecordUid} accodato per l'indicizzazione", record.RecordUid);
        return record.RecordUid.ToString("D");
    }

    private async Task<string?> SaveErrorAsync(
        Record record,
        IDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        // Se la trascrizione è già salvata l'esito di errore viene da un'esecuzione concorrente del job:
        // vale la trascrizione.
        if (record.OriginalTranscript is not null)
        {
            logger.LogWarning("Record {RecordUid} già trascritto: esito di errore ignorato", record.RecordUid);
            return null;
        }

        var errorCode = metadata.TryGetValue(Transcript.ErrorCodeMetadata, out var value) && ErrorCodePattern().IsMatch(value)
            ? value
            : Transcript.ProcessingFailed;

        return await SetErrorAsync(record, errorCode, cancellationToken);
    }

    private async Task<string?> SetErrorAsync(Record record, string errorCode, CancellationToken cancellationToken)
    {
        record.Status = RecordStatus.Error;
        record.ErrorCode = errorCode;

        // Una modifica concorrente del record (RowVersion) solleva un'eccezione: Event Grid riprova.
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Trascrizione del record {RecordUid} non riuscita ({ErrorCode})", record.RecordUid, errorCode);
        return null;
    }

    /// <summary>
    /// L'evento non riporta contenuto e metadati del blob: si scaricano insieme con l'identità gestita.
    /// La trascrizione è in UTF-8 e piccola (al massimo 30 minuti di parlato). Null se il blob non esiste più.
    /// </summary>
    private async Task<BlobDownloadResult?> DownloadAsync(BlobUriBuilder blobUri, CancellationToken cancellationToken)
    {
        var blob = blobService
            .GetBlobContainerClient(blobUri.BlobContainerName)
            .GetBlobClient(blobUri.BlobName);

        try
        {
            var result = await blob.DownloadContentAsync(cancellationToken);
            return result.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>Codice d'errore del job: minuscole e trattini bassi, entro la lunghezza di Records.ErrorCode.</summary>
    [GeneratedRegex("^[a-z_]{1,50}$")]
    private static partial Regex ErrorCodePattern();
}
