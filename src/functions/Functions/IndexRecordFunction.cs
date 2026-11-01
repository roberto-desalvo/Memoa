using Azure.Storage.Queues.Models;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Embeddings;
using Memoa.Functions.Tagging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace Memoa.Functions.Functions;

/// <summary>
/// Ultimo passo di entrambi i flussi, attivato dalla coda di indicizzazione (messaggio: RecordUid in
/// formato "D", scritto da <see cref="EnqueueIndexingFunction"/> per gli audio e dalla Function HTTP per
/// i testi): divide il testo del record in frammenti, ne calcola gli embedding e li salva con lo stato
/// "pronto". Poi accoda il record per l'assegnazione automatica dei tag (<see cref="TagRecordsFunction"/>).
/// Idempotente: la coda consegna almeno una volta, e i chunk del record vengono sostituiti in una
/// transazione, mai aggiunti. Il testo è già nel database: se l'indicizzazione fallisce non va perso.
/// </summary>
public sealed class IndexRecordFunction(
    MemoaDbContext db,
    EmbeddingClient embeddingClient,
    ILogger<IndexRecordFunction> logger)
{
    /// <summary>Tentativi esauriti: il testo resta salvato e l'utente può riprovare.</summary>
    public const string IndexingFailed = "indexing_failed";

    /// <summary>Uguale a extensions.queues.maxDequeueCount in host.json.</summary>
    private const int MaxDequeueCount = 5;

    /// <summary>
    /// Il messaggio per la coda dei tag parte solo se il record è pronto, anche quando lo era già: se la
    /// scrittura sulla coda fallisce dopo il salvataggio, il nuovo tentativo trova il record pronto e la
    /// riprova (l'assegnazione dei tag è idempotente).
    /// </summary>
    [Function("IndexRecord")]
    [QueueOutput("%Queues:Tagging%", Connection = "DataStorage")]
    public async Task<string?> Run(
        [QueueTrigger("%Queues:Indexing%", Connection = "DataStorage")] QueueMessage message,
        CancellationToken cancellationToken)
    {
        // Solo la pipeline scrive su questa coda: un messaggio non valido non diventa valido riprovando.
        if (!Guid.TryParseExact(message.MessageText, "D", out var recordUid))
        {
            logger.LogError("Messaggio {MessageId} non valido sulla coda di indicizzazione: scartato", message.MessageId);
            return null;
        }

        try
        {
            return await IndexAsync(recordUid, cancellationToken)
                ? TaggingRequest.ForRecord(recordUid).Serialize()
                : null;
        }
        catch (Exception ex) when (message.DequeueCount >= MaxDequeueCount && ex is not OperationCanceledException)
        {
            // Ultimo tentativo: senza questo il record resterebbe "in elaborazione" e il frontend
            // continuerebbe il polling. Il messaggio va comunque nella coda dei messaggi non elaborabili.
            logger.LogError(ex, "Indicizzazione del record {RecordUid} non riuscita dopo {DequeueCount} tentativi", recordUid, message.DequeueCount);
            await SetErrorAsync(recordUid, cancellationToken);
            throw;
        }
    }

    /// <summary>Indicizza il record; true se il record è pronto (ora o già da prima).</summary>
    private async Task<bool> IndexAsync(Guid recordUid, CancellationToken cancellationToken)
    {
        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        // Il messaggio viene scritto dopo il record: se manca è stato eliminato dall'utente.
        if (record is null)
        {
            logger.LogWarning("Nessun record {RecordUid}: messaggio ignorato", recordUid);
            return false;
        }

        // Errore o record già pronto: un messaggio duplicato o tardivo non cambia lo stato.
        if (record.Status != RecordStatus.Processing)
        {
            logger.LogInformation("Record {RecordUid} non più in elaborazione: messaggio ignorato", recordUid);
            return record.Status == RecordStatus.Ready;
        }

        // Rigenerazione della trascrizione in corso (RegenerateTranscriptFunction): il testo corrente sta per
        // essere sostituito e EnqueueIndexing accoderà di nuovo il record se cambia. Un messaggio tardivo
        // porterebbe il record a "pronto" e la nuova trascrizione verrebbe ignorata.
        if (record.SourceType == SourceType.Audio && record.OriginalTranscript is null && record.Text is not null)
        {
            logger.LogInformation("Record {RecordUid} in attesa della nuova trascrizione: messaggio ignorato", recordUid);
            return false;
        }

        // Chi accoda garantisce un testo non vuoto: senza testo non c'è nulla da indicizzare.
        if (string.IsNullOrWhiteSpace(record.Text))
        {
            logger.LogError("Record {RecordUid} senza testo: non indicizzabile", recordUid);
            record.Status = RecordStatus.Error;
            record.ErrorCode = IndexingFailed;
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        var contents = TextChunker.Split(record.Text);

        // Una sola richiesta per tutti i frammenti (al massimo circa 30 per 50.000 caratteri). Il client
        // riprova da sé sugli errori temporanei e sui 429; gli altri errori fanno riprovare la coda.
        var embeddings = await embeddingClient.GenerateCheckedEmbeddingsAsync(contents, cancellationToken);

        var chunks = embeddings
            .Select(e => new RecordChunk
            {
                RecordId = record.Id,
                ChunkIndex = e.Index,
                Content = contents[e.Index],
                Embedding = new SqlVector<float>(e.ToFloats()),
                // Il modello restituito dal servizio, non il nome del deployment.
                EmbeddingModel = embeddings.Model,
            })
            .ToList();

        // Con i retry attivi la transazione esplicita va eseguita nella strategia di esecuzione.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Sostituisce i chunk di un'indicizzazione precedente (es. testo modificato dall'utente).
            await db.RecordChunks
                .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
                .Where(c => c.RecordId == record.Id)
                .ExecuteDeleteAsync(ct);

            db.RecordChunks.AddRange(chunks);
            record.Status = RecordStatus.Ready;
            record.ErrorCode = null;

            // Se il testo è cambiato nel frattempo (RowVersion) o un'esecuzione concorrente ha già
            // inserito i chunk (indice univoco) si solleva un'eccezione: la coda riprova sul testo nuovo.
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);

        logger.LogInformation("Record {RecordUid} indicizzato ({ChunkCount} frammenti)", recordUid, chunks.Count);
        return true;
    }

    /// <summary>
    /// Porta il record a "in errore" senza toccare il testo. Aggiornamento diretto, indipendente dalle
    /// entità tracciate dal tentativo fallito; solo se il record è ancora in elaborazione.
    /// </summary>
    private async Task SetErrorAsync(Guid recordUid, CancellationToken cancellationToken)
    {
        try
        {
            await db.Records
                .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
                .Where(r => r.RecordUid == recordUid && r.Status == RecordStatus.Processing)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, RecordStatus.Error)
                    .SetProperty(r => r.ErrorCode, IndexingFailed)
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
