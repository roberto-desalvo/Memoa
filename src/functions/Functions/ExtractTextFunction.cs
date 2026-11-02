using System.Diagnostics;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Queues.Models;
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
/// Terzo passo del flusso dei file di testo, attivato dalla coda di estrazione del testo (l'URL del file
/// originale, scritto da <see cref="ValidateDocumentUploadFunction"/> e da "riprova"): riconosce il formato
/// reale, estrae il testo (<see cref="TextExtractor"/>) e lo salva nel database, in Text e in
/// OriginalTranscript, poi scrive il RecordUid sulla coda di indicizzazione. Da lì il flusso è quello dei
/// testi scritti. Se il file va scartato porta il record a "in errore" con il codice del motivo.
/// Idempotente: la coda consegna almeno una volta; i messaggi di un record non più in elaborazione o con il
/// testo già estratto non rileggono il file.
/// </summary>
public sealed class ExtractTextFunction(
    MemoaDbContext db,
    BlobServiceClient blobService,
    IOptions<DataStorageOptions> storage,
    IOptions<ContainerOptions> containers,
    ILogger<ExtractTextFunction> logger)
{
    /// <summary>Uguale a extensions.queues.maxDequeueCount in host.json.</summary>
    private const int MaxDequeueCount = 5;

    /// <summary>RecordUid in formato "D" per la coda di indicizzazione, oppure null se non c'è nulla da accodare.</summary>
    [Function("ExtractText")]
    [QueueOutput("%Queues:Indexing%", Connection = "DataStorage")]
    public async Task<string?> Run(
        [QueueTrigger("%Queues:TextExtraction%", Connection = "DataStorage")] QueueMessage message,
        CancellationToken cancellationToken)
    {
        // Solo la pipeline scrive su questa coda: un messaggio non valido non diventa valido riprovando.
        if (!TryParseSource(message.MessageText, out var source, out var entraObjectId, out var recordUid))
        {
            // Il contenuto del messaggio non va nei log: si identifica con il suo id.
            logger.LogError("Messaggio {MessageId} non valido sulla coda di estrazione del testo: scartato", message.MessageId);
            return null;
        }

        try
        {
            return await ExtractAsync(source, entraObjectId, recordUid, cancellationToken);
        }
        catch (Exception ex) when (message.DequeueCount >= MaxDequeueCount && ex is not OperationCanceledException)
        {
            // Senza esito il record resterebbe "in elaborazione" e il frontend continuerebbe il polling.
            // Il messaggio va comunque nella coda dei messaggi non elaborabili.
            logger.LogError(ex, "Estrazione del testo del record {RecordUid} non riuscita dopo {DequeueCount} tentativi",
                recordUid, message.DequeueCount);
            await SetErrorAsync(recordUid, cancellationToken);
            throw;
        }
    }

    private async Task<string?> ExtractAsync(
        BlobClient source, Guid entraObjectId, Guid recordUid, CancellationToken cancellationToken)
    {
        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Include(r => r.Attachments.Where(a => a.Kind == AttachmentKind.Document))
            .Where(r => r.RecordUid == recordUid && r.User.EntraObjectId == entraObjectId)
            .SingleOrDefaultAsync(cancellationToken);

        // Il messaggio viene scritto dopo il record: se manca è stato eliminato dall'utente (o il percorso non
        // corrisponde all'utente del record).
        if (record is null)
        {
            logger.LogWarning("Nessun record {RecordUid} per l'utente del blob: messaggio ignorato", recordUid);
            return null;
        }

        if (record.SourceType != SourceType.Text || record.Attachments.Count == 0)
        {
            logger.LogError("Record {RecordUid} senza file di testo allegato: messaggio ignorato", recordUid);
            return null;
        }

        // Errore o record già pronto: un messaggio duplicato o tardivo non cambia lo stato.
        if (record.Status != RecordStatus.Processing)
        {
            logger.LogInformation("Record {RecordUid} non più in elaborazione: messaggio ignorato", recordUid);
            return null;
        }

        // Testo già salvato da una consegna precedente, che può non essere riuscita a scrivere sulla coda di
        // indicizzazione: si accoda di nuovo senza rileggere il file.
        if (record.OriginalTranscript is not null)
        {
            logger.LogInformation("Testo del record {RecordUid} già estratto: accodato per l'indicizzazione", recordUid);
            return record.RecordUid.ToString("D");
        }

        BinaryData content;
        try
        {
            // Al più 1 MB (limite dei PDF, ricontrollato sul formato reale): si legge in memoria.
            content = (await source.DownloadContentAsync(cancellationToken)).Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // L'originale non si cancella mai, salvo eliminazione del record: un'assenza va indagata.
            logger.LogError("Record {RecordUid}: file di testo originale non trovato", recordUid);
            return await SetErrorAsync(record, TextExtractor.ProcessingFailed, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        var result = TextExtractor.Extract(content);

        if (result.ErrorCode is not null)
        {
            logger.LogWarning(
                "Estrazione del record {RecordUid} non riuscita ({ErrorCode}): formato {Format}, {Pages} pagine, {SizeBytes} byte, {Characters} caratteri",
                recordUid, result.ErrorCode, result.Format, result.Pages, content.ToMemory().Length, result.Characters);
            return await SetErrorAsync(record, result.ErrorCode, cancellationToken);
        }

        // OriginalTranscript resta il testo estratto; Text è quello corrente, che l'utente potrà modificare.
        // La lingua resta da definire come per i testi scritti.
        record.OriginalTranscript = result.Text;
        record.Text = result.Text;

        // Una modifica concorrente del record (RowVersion) solleva un'eccezione: la coda riprova.
        await db.SaveChangesAsync(cancellationToken);

        // Nei log solo valori tecnici: mai il testo.
        logger.LogInformation(
            "Testo estratto dal record {RecordUid} in {ElapsedSeconds:0.###} s: formato {Format}, {Pages} pagine, {SizeBytes} byte, {Characters} caratteri",
            recordUid, stopwatch.Elapsed.TotalSeconds, result.Format, result.Pages, content.ToMemory().Length, result.Characters);

        return record.RecordUid.ToString("D");
    }

    /// <summary>Porta il record a "in errore" con il codice del motivo; nulla da accodare.</summary>
    private async Task<string?> SetErrorAsync(Record record, string errorCode, CancellationToken cancellationToken)
    {
        record.Status = RecordStatus.Error;
        record.ErrorCode = errorCode;

        // Una modifica concorrente del record (RowVersion) solleva un'eccezione: la coda riprova.
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    /// <summary>
    /// Porta il record a "in errore" con "processing_failed". Aggiornamento diretto, indipendente dalle
    /// entità tracciate dal tentativo fallito; solo se il record è ancora in elaborazione e senza testo estratto.
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
                    .SetProperty(r => r.ErrorCode, TextExtractor.ProcessingFailed)
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

    /// <summary>
    /// Il messaggio è l'URL del file originale (senza SAS), nella forma
    /// "&lt;servizio blob&gt;/&lt;contenitore dei file di testo&gt;/&lt;oid utente&gt;/&lt;RecordUid&gt;"
    /// (vedi <see cref="RecordBlobService.DocumentBlobUrl"/>). Si accettano solo blob dello storage dei dati
    /// configurato, letti con il client di quello storage.
    /// </summary>
    private bool TryParseSource(string body, out BlobClient source, out Guid entraObjectId, out Guid recordUid)
    {
        source = null!;
        entraObjectId = recordUid = Guid.Empty;

        if (!Uri.TryCreate(body, UriKind.Absolute, out var uri)
            || Uri.Compare(uri, storage.Value.BlobServiceUri, UriComponents.SchemeAndServer,
                UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0
            || !string.IsNullOrEmpty(uri.Query))
        {
            return false;
        }

        var builder = new BlobUriBuilder(uri);
        if (builder.BlobContainerName != containers.Value.Documents
            || !RecordBlobService.TryParseUploadBlobName(builder.BlobName, out entraObjectId, out recordUid))
        {
            return false;
        }

        source = blobService.GetBlobContainerClient(builder.BlobContainerName).GetBlobClient(builder.BlobName);
        return true;
    }
}
