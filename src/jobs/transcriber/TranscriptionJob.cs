using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Memoa.Shared.Pipeline;
using Memoa.Transcriber.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Transcriber;

/// <summary>
/// Sesto passo del flusso audio: legge un messaggio dalla coda di trascrizione (l'URL della copia
/// normalizzata e la lingua dell'account), trascrive l'audio e scrive nel contenitore delle trascrizioni
/// il testo oppure, se la trascrizione non riesce, l'esito con il solo codice d'errore (vedi
/// <see cref="Transcript"/>).
/// L'evento "blob creato" su quel contenitore fa proseguire il flusso: il job non scrive né nel
/// database né sulle code. La copia normalizzata viene solo letta.
/// Idempotente: se uno dei due blob esiste già il messaggio viene solo eliminato.
/// </summary>
public sealed class TranscriptionJob(
    QueueClient queue,
    BlobServiceClient blobService,
    ITranscriptionEngine engine,
    IOptions<DataStorageOptions> storage,
    IOptions<JobOptions> options,
    ILogger<TranscriptionJob> logger)
{
    /// <summary>
    /// Elabora al più un messaggio: ogni esecuzione del job viene avviata dallo scaler KEDA per un
    /// messaggio in coda. Un'eccezione lascia il messaggio in coda, che torna visibile dopo il
    /// VisibilityTimeout per un nuovo tentativo.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var job = options.Value;
        QueueMessage? message = await queue.ReceiveMessageAsync(job.VisibilityTimeout, cancellationToken);

        // Lo scaler può avviare più esecuzioni di quanti siano i messaggi visibili.
        if (message is null)
        {
            logger.LogInformation("Nessun messaggio in coda");
            return;
        }

        if (!TryParseRequest(message.Body.ToString(), out var source, out var fallbackLanguage, out var entraObjectId, out var recordUid))
        {
            // Il contenuto del messaggio non va nei log: si identifica con il suo id.
            logger.LogError("Messaggio {MessageId} non valido: eliminato", message.MessageId);
            await DeleteAsync(message, cancellationToken);
            return;
        }

        var output = blobService.GetBlobContainerClient(job.OutputContainer);
        var transcript = output.GetBlobClient(Transcript.TextBlobName(entraObjectId, recordUid));
        var error = output.GetBlobClient(Transcript.ErrorBlobName(entraObjectId, recordUid));

        if (await transcript.ExistsAsync(cancellationToken) || await error.ExistsAsync(cancellationToken))
        {
            logger.LogInformation("Record {RecordUid} già trascritto: messaggio duplicato", recordUid);
            await DeleteAsync(message, cancellationToken);
            return;
        }

        if (message.DequeueCount > job.MaxDequeueCount)
        {
            logger.LogError("Record {RecordUid}: trascrizione fallita dopo {Attempts} tentativi", recordUid, job.MaxDequeueCount);
            await WriteErrorAsync(error, Transcript.ProcessingFailed, cancellationToken);
            await DeleteAsync(message, cancellationToken);
            return;
        }

        var workDirectory = Directory.CreateTempSubdirectory("transcriber-");
        try
        {
            var errorCode = await TranscribeAsync(source, transcript, fallbackLanguage, recordUid, workDirectory.FullName, cancellationToken);
            if (errorCode is not null)
            {
                logger.LogWarning("Record {RecordUid} non trascritto ({ErrorCode})", recordUid, errorCode);
                await WriteErrorAsync(error, errorCode, cancellationToken);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            // L'utente ha eliminato il record o l'account: nessun esito da scrivere, altrimenti
            // resterebbero blob senza record.
            logger.LogWarning("Record {RecordUid}: copia normalizzata non trovata, messaggio eliminato", recordUid);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }

        await DeleteAsync(message, cancellationToken);
    }

    /// <summary>Restituisce il codice d'errore se la trascrizione va scartata, null se il testo è stato scritto.</summary>
    private async Task<string?> TranscribeAsync(
        BlobClient source,
        BlobClient transcript,
        string fallbackLanguage,
        Guid recordUid,
        string workDirectory,
        CancellationToken cancellationToken)
    {
        var inputPath = Path.Combine(workDirectory, "input.opus");
        await source.DownloadToAsync(inputPath, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var result = await engine.TranscribeAsync(inputPath, fallbackLanguage, cancellationToken);
        stopwatch.Stop();

        if (string.IsNullOrWhiteSpace(result.Text))
        {
            return Transcript.NoSpeech;
        }

        var metadata = new Dictionary<string, string>
        {
            [Transcript.LanguageMetadata] = result.Language,
            [Transcript.EngineMetadata] = engine.Name,
        };
        if (result.DetectedLanguage is not null)
        {
            metadata[Transcript.DetectedLanguageMetadata] = result.DetectedLanguage;
        }
        if (result.LanguageProbability is { } probability)
        {
            metadata[Transcript.LanguageProbabilityMetadata] = probability.ToString("0.###", CultureInfo.InvariantCulture);
        }

        await UploadIfMissingAsync(transcript, BinaryData.FromString(result.Text).ToStream(), new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain; charset=utf-8" },
            Metadata = metadata,
        }, cancellationToken);

        // Nei log solo valori tecnici: mai il testo.
        logger.LogInformation(
            "Record {RecordUid} trascritto in {ElapsedSeconds:0} s: lingua {Language} (rilevata {DetectedLanguage}, probabilità {LanguageProbability:0.###}), {Characters} caratteri",
            recordUid, stopwatch.Elapsed.TotalSeconds, result.Language, result.DetectedLanguage, result.LanguageProbability, result.Text.Length);
        return null;
    }

    private Task WriteErrorAsync(BlobClient error, string errorCode, CancellationToken cancellationToken)
    {
        var content = BinaryData.FromObjectAsJson(new { errorCode }, JsonSerializerOptions.Web);

        return UploadIfMissingAsync(error, content.ToStream(), new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
            Metadata = new Dictionary<string, string> { [Transcript.ErrorCodeMetadata] = errorCode },
        }, cancellationToken);
    }

    /// <summary>
    /// Scrive il blob solo se non esiste: un messaggio duplicato elaborato in parallelo non genera
    /// un secondo evento "blob creato".
    /// </summary>
    private async Task UploadIfMissingAsync(BlobClient blob, Stream content, BlobUploadOptions uploadOptions, CancellationToken cancellationToken)
    {
        uploadOptions.Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All };

        try
        {
            await blob.UploadAsync(content, uploadOptions, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            logger.LogInformation("Blob già scritto da un'esecuzione concorrente");
        }
    }

    private Task DeleteAsync(QueueMessage message, CancellationToken cancellationToken) =>
        queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);

    /// <summary>
    /// Il messaggio è un <see cref="TranscriptionRequest"/> con l'URL della copia normalizzata (senza
    /// SAS), nella forma "&lt;servizio blob&gt;/&lt;contenitore dell'audio normalizzato&gt;/&lt;oid utente&gt;/&lt;RecordUid&gt;.opus"
    /// (vedi EnqueueTranscription). Si accettano solo blob dello storage dei dati configurato: l'identità
    /// del job non deve autenticarsi verso altri account.
    /// </summary>
    private bool TryParseRequest(
        string body,
        out BlobClient source,
        out string fallbackLanguage,
        out Guid entraObjectId,
        out Guid recordUid)
    {
        source = null!;
        fallbackLanguage = string.Empty;
        entraObjectId = recordUid = Guid.Empty;

        if (!TranscriptionRequest.TryParse(body, out var request)
            || !Uri.TryCreate(request.BlobUrl, UriKind.Absolute, out var uri)
            || Uri.Compare(uri, storage.Value.BlobServiceUri, UriComponents.SchemeAndServer,
                UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0
            || !string.IsNullOrEmpty(uri.Query))
        {
            return false;
        }

        var builder = new BlobUriBuilder(uri);
        if (builder.BlobContainerName != options.Value.InputContainer
            || !NormalizedAudio.TryParse(builder.BlobName, out entraObjectId, out recordUid, out var isError)
            || isError)
        {
            return false;
        }

        fallbackLanguage = request.FallbackLanguage;
        source = blobService.GetBlobContainerClient(builder.BlobContainerName).GetBlobClient(builder.BlobName);
        return true;
    }
}
