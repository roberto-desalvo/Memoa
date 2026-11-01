using System.Diagnostics;
using System.Net;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Queues.Models;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Normalization;
using Memoa.Functions.Storage;
using Memoa.Functions.Transcription;
using Memoa.Shared.Pipeline;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Functions;

/// <summary>
/// Sesto passo del flusso audio, attivato dalla coda di trascrizione (<see cref="TranscriptionRequest"/>,
/// scritto da <see cref="EnqueueTranscriptionFunction"/>, da "riprova" e dalla rigenerazione): invia la copia
/// normalizzata ad Azure AI Speech e salva testo e lingua direttamente nel database, senza blob della
/// trascrizione, poi scrive il RecordUid sulla coda di indicizzazione.
/// Se il servizio non risponde entro il timeout, si superano i limiti o falliscono tutti i tentativi, il
/// messaggio va invece sulla coda di ripiego, letta dal job di trascrizione con Whisper; il flusso prosegue
/// dal blob della trascrizione (<see cref="EnqueueIndexingFunction"/>).
/// Idempotente: la coda consegna almeno una volta; i messaggi di un record non più in elaborazione o già
/// trascritto non richiamano il servizio.
/// </summary>
public sealed class TranscribeRecordFunction(
    MemoaDbContext db,
    BlobServiceClient blobService,
    ITranscriber transcriber,
    TranscriptStore transcripts,
    IOptions<ContainerOptions> containers,
    IOptions<AudioOptions> audioOptions,
    ILogger<TranscribeRecordFunction> logger)
{
    /// <summary>Uguale a extensions.queues.maxDequeueCount in host.json.</summary>
    private const int MaxDequeueCount = 5;

    // Timeout proporzionale alla durata: Microsoft non garantisce una latenza, ma dichiara circa 15 secondi
    // per 10 minuti di audio.
    private static readonly TimeSpan BaseTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TimeoutPerAudioMinute = TimeSpan.FromSeconds(10);

    [Function("TranscribeRecord")]
    public async Task<TranscriptionOutput> Run(
        [QueueTrigger("%Queues:Transcription%", Connection = "DataStorage")] QueueMessage message,
        CancellationToken cancellationToken)
    {
        // Solo la pipeline scrive su questa coda: un messaggio non valido non diventa valido riprovando.
        if (!TryParse(message.MessageText, out var blobUri, out var entraObjectId, out var recordUid))
        {
            // Il contenuto del messaggio non va nei log: si identifica con il suo id.
            logger.LogError("Messaggio {MessageId} non valido sulla coda di trascrizione: scartato", message.MessageId);
            return TranscriptionOutput.None;
        }

        try
        {
            return await TranscribeAsync(blobUri, entraObjectId, recordUid, message.DequeueCount, cancellationToken);
        }
        catch (Exception ex) when (message.DequeueCount >= MaxDequeueCount && ex is not OperationCanceledException)
        {
            // Errori fuori dalla chiamata al servizio (database, lettura dell'audio), che il job di ripiego
            // non risolverebbe. Senza questo il record resterebbe "in elaborazione" e il frontend
            // continuerebbe il polling. Il messaggio va comunque nella coda dei messaggi non elaborabili.
            logger.LogError(ex, "Trascrizione del record {RecordUid} non riuscita dopo {DequeueCount} tentativi", recordUid, message.DequeueCount);
            await SetErrorAsync(recordUid, cancellationToken);
            throw;
        }
    }

    private async Task<TranscriptionOutput> TranscribeAsync(
        BlobUriBuilder blobUri,
        Guid entraObjectId,
        Guid recordUid,
        long dequeueCount,
        CancellationToken cancellationToken)
    {
        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Include(r => r.User)
            .Include(r => r.Attachments.Where(a => a.Kind == AttachmentKind.Audio))
            .Where(r => r.RecordUid == recordUid && r.User.EntraObjectId == entraObjectId)
            .SingleOrDefaultAsync(cancellationToken);

        // Il messaggio viene scritto dopo il record: se manca è stato eliminato dall'utente (o il percorso non
        // corrisponde all'utente del record).
        if (record is null)
        {
            logger.LogWarning("Nessun record {RecordUid} per l'utente del blob: messaggio ignorato", recordUid);
            return TranscriptionOutput.None;
        }

        var attachment = record.Attachments.SingleOrDefault();
        if (record.SourceType != SourceType.Audio || attachment is null)
        {
            logger.LogError("Record {RecordUid} senza allegato audio: messaggio ignorato", recordUid);
            return TranscriptionOutput.None;
        }

        // Errore o record già pronto: un messaggio duplicato o tardivo non cambia lo stato.
        if (record.Status != RecordStatus.Processing)
        {
            logger.LogInformation("Record {RecordUid} non più in elaborazione: messaggio ignorato", recordUid);
            return TranscriptionOutput.None;
        }

        // Trascrizione già salvata da una consegna precedente, che può non essere riuscita a scrivere sulla
        // coda di indicizzazione: si accoda di nuovo senza richiamare il servizio.
        if (record.OriginalTranscript is not null)
        {
            logger.LogInformation("Record {RecordUid} già trascritto: accodato per l'indicizzazione", recordUid);
            return new TranscriptionOutput { IndexingMessage = record.RecordUid.ToString("D") };
        }

        BinaryData audio;
        try
        {
            // Copia Ogg Opus a 32 kbps: circa 7,5 MB per 30 minuti, si legge in memoria.
            audio = (await blobService
                .GetBlobContainerClient(blobUri.BlobContainerName)
                .GetBlobClient(blobUri.BlobName)
                .DownloadContentAsync(cancellationToken)).Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Il record esiste ma la copia no: nemmeno il job di ripiego potrebbe trascriverla.
            logger.LogError("Record {RecordUid}: copia normalizzata non trovata", recordUid);
            await transcripts.SetErrorAsync(record, Transcript.AudioDownloadFailed, cancellationToken);
            return TranscriptionOutput.None;
        }

        var fallback = new TranscriptionOutput
        {
            // Stesso contratto della coda di trascrizione: il job non legge il database.
            FallbackMessage = new TranscriptionRequest(blobUri.ToUri().ToString(), record.User.PreferredLanguage).Serialize(),
        };

        // Se manca la durata della copia normalizzata si usa il massimo accettato.
        var durationMinutes = attachment.DurationSeconds / 60.0 ?? audioOptions.Value.MaxDurationMinutes;
        var timeout = BaseTimeout + TimeoutPerAudioMinute * durationMinutes;
        var stopwatch = Stopwatch.StartNew();
        TranscriptionResult result;

        using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeoutSource.CancelAfter(timeout);
            try
            {
                result = await transcriber.TranscribeAsync(audio, record.User.PreferredLanguage, timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Record {RecordUid}: {Transcriber} senza risposta entro {TimeoutSeconds:0} s, ripiego sul job",
                    recordUid, transcriber.Name, timeout.TotalSeconds);
                return fallback;
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestEntityTooLarge)
            {
                logger.LogWarning(ex, "Record {RecordUid}: limiti di {Transcriber} superati, ripiego sul job", recordUid, transcriber.Name);
                return fallback;
            }
            catch (Exception ex) when (dequeueCount >= MaxDequeueCount && ex is not OperationCanceledException)
            {
                // Prima dell'ultimo tentativo l'eccezione fa riprovare la coda (es. errori 5xx o di rete).
                logger.LogError(ex, "Record {RecordUid}: {Transcriber} non riuscito dopo {DequeueCount} tentativi, ripiego sul job",
                    recordUid, transcriber.Name, dequeueCount);
                return fallback;
            }
        }

        // Nei log solo valori tecnici: mai il testo.
        logger.LogInformation(
            "Record {RecordUid} trascritto da {Transcriber} in {ElapsedSeconds:0.#} s: lingua {Language}, {Characters} caratteri",
            recordUid, transcriber.Name, stopwatch.Elapsed.TotalSeconds, result.Language, result.Text.Length);

        // Una modifica concorrente del record (RowVersion) solleva un'eccezione: la coda riprova.
        return new TranscriptionOutput
        {
            IndexingMessage = await transcripts.SaveTranscriptAsync(record, result.Text, result.Language, cancellationToken),
        };
    }

    /// <summary>
    /// Il messaggio è un <see cref="TranscriptionRequest"/> con l'URL della copia normalizzata (senza SAS),
    /// nel contenitore dell'audio normalizzato. Il blob si legge con il client dello storage dei dati, mai
    /// dall'URL: l'host dell'URL non viene usato.
    /// </summary>
    private bool TryParse(string body, out BlobUriBuilder blobUri, out Guid entraObjectId, out Guid recordUid)
    {
        blobUri = null!;
        entraObjectId = recordUid = Guid.Empty;

        if (!TranscriptionRequest.TryParse(body, out var request)
            || !Uri.TryCreate(request.BlobUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        blobUri = new BlobUriBuilder(uri);
        return blobUri.BlobContainerName == containers.Value.Audio
            && NormalizedAudio.TryParse(blobUri.BlobName, out entraObjectId, out recordUid, out var isError)
            && !isError;
    }

    /// <summary>
    /// Porta il record a "in errore" con "processing_failed". Aggiornamento diretto, indipendente dalle
    /// entità tracciate dal tentativo fallito; solo se il record è ancora in elaborazione e senza trascrizione.
    /// </summary>
    private async Task SetErrorAsync(Guid recordUid, CancellationToken cancellationToken)
    {
        try
        {
            await db.Records
                .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
                .Where(r => r.RecordUid == recordUid && r.Status == RecordStatus.Processing && r.OriginalTranscript == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, RecordStatus.Error)
                    .SetProperty(r => r.ErrorCode, Transcript.ProcessingFailed)
                    // ExecuteUpdate non passa da SaveChanges, che imposta UpdatedAt.
                    .SetProperty(r => r.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            // Es. database non raggiungibile: si propaga l'errore originale.
            logger.LogError(ex, "Impossibile portare il record {RecordUid} in errore", recordUid);
        }
    }
}

/// <summary>
/// Esito di <see cref="TranscribeRecordFunction"/>: al più uno dei messaggi. Con entrambi null non si
/// accoda nulla.
/// </summary>
public sealed class TranscriptionOutput
{
    public static TranscriptionOutput None => new();

    /// <summary>RecordUid in formato "D", come in <see cref="EnqueueIndexingFunction"/>.</summary>
    [QueueOutput("%Queues:Indexing%", Connection = "DataStorage")]
    public string? IndexingMessage { get; init; }

    /// <summary><see cref="TranscriptionRequest"/> in JSON per il job di trascrizione con Whisper.</summary>
    [QueueOutput("%Queues:TranscriptionFallback%", Connection = "DataStorage")]
    public string? FallbackMessage { get; init; }
}
