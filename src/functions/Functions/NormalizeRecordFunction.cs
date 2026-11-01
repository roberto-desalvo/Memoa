using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues.Models;
using Memoa.Functions.Normalization;
using Memoa.Functions.Storage;
using Memoa.Shared.Pipeline;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Functions;

/// <summary>
/// Quarto passo del flusso audio, attivato dalla coda di normalizzazione (l'URL del file originale, scritto
/// da <see cref="ValidateUploadFunction"/> e da "riprova"): verifica il formato reale con ffmpeg e scrive nel
/// contenitore dell'audio normalizzato la copia Ogg Opus oppure, se il file va scartato, l'esito con il solo
/// codice d'errore (vedi <see cref="NormalizedAudio"/>).
/// Il flusso prosegue dall'evento "blob creato" su quel contenitore (<see cref="EnqueueTranscriptionFunction"/>),
/// come quando a normalizzare era il Container Apps Job: nessun accesso al database. L'originale viene solo letto.
/// Idempotente: se uno dei due blob esiste già il messaggio viene solo consumato.
/// </summary>
public sealed class NormalizeRecordFunction(
    BlobServiceClient blobService,
    Ffmpeg ffmpeg,
    IOptions<DataStorageOptions> storage,
    IOptions<ContainerOptions> containers,
    ILogger<NormalizeRecordFunction> logger)
{
    /// <summary>Uguale a extensions.queues.maxDequeueCount in host.json.</summary>
    private const int MaxDequeueCount = 5;

    /// <summary>
    /// Una normalizzazione alla volta per istanza: lo spazio temporaneo di Flex Consumption (0,8 GB) basta
    /// per un originale da 350 MB, la copia e ffmpeg, non per più file in parallelo. Gli altri messaggi
    /// aspettano (l'host ne rinnova la visibilità) o vengono presi da altre istanze.
    /// </summary>
    private static readonly SemaphoreSlim Slot = new(1, 1);

    [Function("NormalizeRecord")]
    public async Task Run(
        [QueueTrigger("%Queues:Normalization%", Connection = "DataStorage")] QueueMessage message,
        CancellationToken cancellationToken)
    {
        // Solo la pipeline scrive su questa coda: un messaggio non valido non diventa valido riprovando.
        if (!TryParseSource(message.MessageText, out var source, out var entraObjectId, out var recordUid))
        {
            // Il contenuto del messaggio non va nei log: si identifica con il suo id.
            logger.LogError("Messaggio {MessageId} non valido sulla coda di normalizzazione: scartato", message.MessageId);
            return;
        }

        var output = blobService.GetBlobContainerClient(containers.Value.Audio);
        var normalized = output.GetBlobClient(NormalizedAudio.AudioBlobName(entraObjectId, recordUid));
        var error = output.GetBlobClient(NormalizedAudio.ErrorBlobName(entraObjectId, recordUid));

        if (await normalized.ExistsAsync(cancellationToken) || await error.ExistsAsync(cancellationToken))
        {
            logger.LogInformation("Record {RecordUid} già elaborato: messaggio duplicato", recordUid);
            return;
        }

        await Slot.WaitAsync(cancellationToken);
        DirectoryInfo? workDirectory = null;
        try
        {
            workDirectory = Directory.CreateTempSubdirectory("normalizer-");
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
            logger.LogWarning("Record {RecordUid}: originale non trovato, messaggio ignorato", recordUid);
        }
        catch (Exception ex) when (message.DequeueCount >= MaxDequeueCount && ex is not OperationCanceledException)
        {
            // Senza esito il record resterebbe "in elaborazione" e il frontend continuerebbe il polling.
            // Il messaggio va comunque nella coda dei messaggi non elaborabili.
            logger.LogError(ex, "Record {RecordUid}: normalizzazione fallita dopo {DequeueCount} tentativi", recordUid, message.DequeueCount);
            await WriteErrorAsync(error, AudioValidation.ProcessingFailed, cancellationToken);
            throw;
        }
        finally
        {
            workDirectory?.Delete(recursive: true);
            Slot.Release();
        }
    }

    /// <summary>Restituisce il codice d'errore se il file va scartato, null se la copia è stata scritta.</summary>
    private async Task<string?> NormalizeAsync(
        BlobClient source,
        BlobClient normalized,
        Guid recordUid,
        string workDirectory,
        CancellationToken cancellationToken)
    {
        // ffmpeg legge da file: alcuni formati (es. M4A con l'indice in fondo) non si possono elaborare
        // in streaming.
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
        // L'originale non serve più: libera lo spazio temporaneo prima del caricamento.
        File.Delete(inputPath);

        var outputProbe = await ffmpeg.ProbeAsync(outputPath, cancellationToken)
            ?? throw new InvalidOperationException("ffmpeg non riesce a leggere la copia normalizzata.");

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
                [NormalizedAudio.DurationMetadata] = duration.ToString(CultureInfo.InvariantCulture),
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

    /// <summary>
    /// Il messaggio è l'URL del blob originale (senza SAS), nella forma
    /// "&lt;servizio blob&gt;/&lt;contenitore degli originali&gt;/&lt;oid utente&gt;/&lt;RecordUid&gt;"
    /// (vedi <see cref="RecordBlobService.UploadBlobUrl"/>). Si accettano solo blob dello storage dei dati
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
        if (builder.BlobContainerName != containers.Value.Uploads)
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
