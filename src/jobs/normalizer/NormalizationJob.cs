using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Memoa.Normalizer.Audio;
using Memoa.Shared.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Normalizer;

/// <summary>
/// Quarto passo del flusso audio: legge un messaggio dalla coda di normalizzazione (l'URL del file
/// originale), verifica il formato reale con ffprobe e scrive nel contenitore dell'audio normalizzato:
/// la copia Ogg Opus oppure, se il file va scartato, l'esito con il solo codice d'errore (vedi
/// <see cref="NormalizedAudio"/>).
/// L'evento "blob creato" su quel contenitore fa proseguire il flusso: il job non scrive né nel
/// database né sulle code. L'originale viene solo letto.
/// Idempotente: se uno dei due blob esiste già il messaggio viene solo eliminato.
/// </summary>
public sealed class NormalizationJob(
    QueueClient queue,
    BlobServiceClient blobService,
    Ffmpeg ffmpeg,
    IOptions<DataStorageOptions> storage,
    IOptions<JobOptions> options,
    ILogger<NormalizationJob> logger)
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

        if (!TryParseSource(message.Body.ToString(), out var source, out var entraObjectId, out var recordUid))
        {
            // Il contenuto del messaggio non va nei log: si identifica con il suo id.
            logger.LogError("Messaggio {MessageId} non valido: eliminato", message.MessageId);
            await DeleteAsync(message, cancellationToken);
            return;
        }

        var output = blobService.GetBlobContainerClient(job.OutputContainer);
        var normalized = output.GetBlobClient(NormalizedAudio.AudioBlobName(entraObjectId, recordUid));
        var error = output.GetBlobClient(NormalizedAudio.ErrorBlobName(entraObjectId, recordUid));

        if (await normalized.ExistsAsync(cancellationToken) || await error.ExistsAsync(cancellationToken))
        {
            logger.LogInformation("Record {RecordUid} già elaborato: messaggio duplicato", recordUid);
            await DeleteAsync(message, cancellationToken);
            return;
        }

        if (message.DequeueCount > job.MaxDequeueCount)
        {
            logger.LogError("Record {RecordUid}: normalizzazione fallita dopo {Attempts} tentativi", recordUid, job.MaxDequeueCount);
            await WriteErrorAsync(error, AudioValidation.ProcessingFailed, cancellationToken);
            await DeleteAsync(message, cancellationToken);
            return;
        }

        var workDirectory = Directory.CreateTempSubdirectory("normalizer-");
        try
        {
            var errorCode = await NormalizeAsync(source, normalized, recordUid, workDirectory.FullName, cancellationToken);
            if (errorCode is not null)
            {
                logger.LogWarning("Record {RecordUid} scartato ({ErrorCode})", recordUid, errorCode);
                await WriteErrorAsync(error, errorCode, cancellationToken);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            // L'utente ha eliminato il record o l'account: nessun esito da scrivere, altrimenti
            // resterebbero blob senza record.
            logger.LogWarning("Record {RecordUid}: originale non trovato, messaggio eliminato", recordUid);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }

        await DeleteAsync(message, cancellationToken);
    }

    /// <summary>Restituisce il codice d'errore se il file va scartato, null se la copia è stata scritta.</summary>
    private async Task<string?> NormalizeAsync(
        BlobClient source,
        BlobClient normalized,
        Guid recordUid,
        string workDirectory,
        CancellationToken cancellationToken)
    {
        // ffprobe e ffmpeg leggono da file: alcuni formati (es. M4A con l'indice in fondo) non si
        // possono elaborare in streaming.
        var inputPath = Path.Combine(workDirectory, "input");
        var outputPath = Path.Combine(workDirectory, "output.opus");

        await source.DownloadToAsync(inputPath, cancellationToken);

        var sourceProbe = await ffmpeg.ProbeAsync(inputPath, cancellationToken);
        if (sourceProbe is null)
        {
            return AudioValidation.FormatNotSupported;
        }

        var sourceError = AudioValidation.ValidateSource(sourceProbe);
        if (sourceError is not null)
        {
            return sourceError;
        }

        await ffmpeg.TranscodeToOpusAsync(inputPath, outputPath, cancellationToken);

        var outputProbe = await ffmpeg.ProbeAsync(outputPath, cancellationToken)
            ?? throw new InvalidOperationException("ffprobe non riesce a leggere la copia normalizzata.");

        var outputError = AudioValidation.ValidateNormalized(outputProbe);
        if (outputError is not null)
        {
            return outputError;
        }

        var duration = (int)Math.Ceiling(outputProbe.DurationSeconds!.Value);

        await using var stream = File.OpenRead(outputPath);
        await UploadIfMissingAsync(normalized, stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "audio/ogg; codecs=opus" },
            Metadata = new Dictionary<string, string>
            {
                [NormalizedAudio.DurationMetadata] = duration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [NormalizedAudio.SourceFormatMetadata] = sourceProbe.FormatName,
            },
        }, cancellationToken);

        logger.LogInformation("Record {RecordUid} normalizzato: {DurationSeconds} s, {SizeBytes} byte",
            recordUid, duration, stream.Length);
        return null;
    }

    private Task WriteErrorAsync(BlobClient error, string errorCode, CancellationToken cancellationToken)
    {
        var content = BinaryData.FromObjectAsJson(new { errorCode }, JsonSerializerOptions.Web);

        return UploadIfMissingAsync(error, content.ToStream(), new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
            Metadata = new Dictionary<string, string> { [NormalizedAudio.ErrorCodeMetadata] = errorCode },
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
    /// Il messaggio è l'URL del blob originale (senza SAS), nella forma
    /// "&lt;servizio blob&gt;/&lt;contenitore degli originali&gt;/&lt;oid utente&gt;/&lt;RecordUid&gt;"
    /// (vedi ValidateUpload). Si accettano solo blob dello storage dei dati configurato: l'identità
    /// del job non deve autenticarsi verso altri account.
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
        if (builder.BlobContainerName != options.Value.InputContainer)
        {
            return false;
        }

        var parts = builder.BlobName.Split('/');
        if (parts.Length != 2
            || !Guid.TryParseExact(parts[0], "D", out entraObjectId)
            || !Guid.TryParseExact(parts[1], "D", out recordUid))
        {
            return false;
        }

        source = blobService.GetBlobContainerClient(builder.BlobContainerName).GetBlobClient(builder.BlobName);
        return true;
    }
}
