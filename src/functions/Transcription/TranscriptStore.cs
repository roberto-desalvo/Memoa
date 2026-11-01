using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Shared.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Transcription;

/// <summary>
/// Salva nel record l'esito della trascrizione automatica, comune ai due percorsi: Azure AI Speech
/// (TranscribeRecordFunction) e job di ripiego (EnqueueIndexingFunction, dal blob della trascrizione).
/// Il record va caricato con l'utente, per la lingua dell'account.
/// </summary>
public sealed class TranscriptStore(MemoaDbContext db, ILogger<TranscriptStore> logger)
{
    /// <summary>
    /// Salva testo e lingua e restituisce il messaggio per la coda di indicizzazione (il RecordUid in
    /// formato "D"), oppure null se non c'è nulla da accodare.
    /// Una modifica concorrente del record (RowVersion) solleva un'eccezione: il chiamante riprova.
    /// </summary>
    /// <param name="language">Lingua della trascrizione; se manca o non è supportata vale quella dell'account.</param>
    public async Task<string?> SaveTranscriptAsync(Record record, string text, string? language, CancellationToken cancellationToken)
    {
        // Una trascrizione vuota è inutilizzabile per l'indicizzazione.
        if (string.IsNullOrWhiteSpace(text))
        {
            logger.LogWarning("Record {RecordUid}: trascrizione vuota", record.RecordUid);
            return await SetErrorAsync(record, Transcript.NoSpeech, cancellationToken);
        }

        if (record.OriginalTranscript is null)
        {
            // Prima trascrizione, oppure rigenerazione (RegenerateTranscriptFunction azzera OriginalTranscript
            // e lascia il testo corrente, già indicizzato): se il testo non cambia l'indicizzazione non serve.
            var unchanged = record.Text == text && await HasChunksAsync(record, cancellationToken);

            // OriginalTranscript resta la trascrizione automatica; Text è quella corrente, che l'utente
            // potrà modificare. La rigenerazione sostituisce entrambe.
            record.OriginalTranscript = text;
            record.Text = text;

            if (language is not null && TranscriptionRequest.SupportedLanguages.Contains(language))
            {
                record.Language = language;
            }
            else
            {
                logger.LogWarning("Record {RecordUid}: lingua della trascrizione mancante o non valida", record.RecordUid);
                record.Language = record.User.PreferredLanguage;
            }

            if (unchanged)
            {
                record.Status = RecordStatus.Ready;
                record.ErrorCode = null;
                // Stesso testo: i tag assegnati restano validi, nessun confronto da attendere.
                record.TagsPendingSince = null;
            }

            await db.SaveChangesAsync(cancellationToken);

            if (unchanged)
            {
                logger.LogInformation("Record {RecordUid}: trascrizione rigenerata invariata, indicizzazione non necessaria", record.RecordUid);
                return null;
            }
        }

        // Si accoda anche alle consegne successive finché il record è in elaborazione: la prima può aver
        // salvato la trascrizione senza riuscire a scrivere sulla coda. La Function di indicizzazione è idempotente.
        logger.LogInformation("Record {RecordUid} accodato per l'indicizzazione", record.RecordUid);
        return record.RecordUid.ToString("D");
    }

    /// <summary>Porta il record a "in errore" con il codice della fase non riuscita; nulla da accodare.</summary>
    public async Task<string?> SetErrorAsync(Record record, string errorCode, CancellationToken cancellationToken)
    {
        record.Status = RecordStatus.Error;
        record.ErrorCode = errorCode;

        // Una modifica concorrente del record (RowVersion) solleva un'eccezione: il chiamante riprova.
        await db.SaveChangesAsync(cancellationToken);

        // Un errore che non dipende dall'audio indica un problema del sistema: livello Error per gli
        // avvisi di Application Insights. La causa è nei log della trascrizione con lo stesso RecordUid.
        var level = Transcript.IsContentError(errorCode) ? LogLevel.Warning : LogLevel.Error;
        logger.Log(level, "Trascrizione del record {RecordUid} non riuscita ({ErrorCode})", record.RecordUid, errorCode);
        return null;
    }

    /// <summary>
    /// True se il testo corrente è già indicizzato: IndexRecordFunction salva i chunk insieme allo stato
    /// "pronto", quindi esistono solo dopo un'indicizzazione completata.
    /// </summary>
    private Task<bool> HasChunksAsync(Record record, CancellationToken cancellationToken) =>
        db.RecordChunks
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .AnyAsync(c => c.RecordId == record.Id, cancellationToken);
}
