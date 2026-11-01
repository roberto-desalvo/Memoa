using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Memoa.Functions.Functions;

/// <summary>
/// Risposta HTTP più l'eventuale messaggio per la coda di indicizzazione (il RecordUid in formato "D",
/// come in <see cref="EnqueueIndexingFunction"/>). Con un messaggio null non si accoda nulla.
/// </summary>
public sealed class HttpWithIndexingOutput
{
    [HttpResult]
    public required IActionResult Result { get; init; }

    [QueueOutput("%Queues:Indexing%", Connection = "DataStorage")]
    public string? IndexingMessage { get; init; }
}

/// <summary>
/// Risposta HTTP più l'eventuale messaggio per la coda di trascrizione
/// (<see cref="Memoa.Shared.Pipeline.TranscriptionRequest"/> in JSON, come in
/// <see cref="EnqueueTranscriptionFunction"/>), letta da <see cref="TranscribeRecordFunction"/>. Con un
/// messaggio null non si accoda nulla.
/// </summary>
public sealed class HttpWithTranscriptionOutput
{
    [HttpResult]
    public required IActionResult Result { get; init; }

    [QueueOutput("%Queues:Transcription%", Connection = "DataStorage")]
    public string? TranscriptionMessage { get; init; }
}

/// <summary>
/// Risposta HTTP più l'eventuale messaggio per la coda dei tag
/// (<see cref="Memoa.Functions.Tagging.TaggingRequest"/> in JSON), letta da <see cref="TagRecordsFunction"/>.
/// Con un messaggio null non si accoda nulla.
/// </summary>
public sealed class HttpWithTaggingOutput
{
    [HttpResult]
    public required IActionResult Result { get; init; }

    [QueueOutput("%Queues:Tagging%", Connection = "DataStorage")]
    public string? TaggingMessage { get; init; }
}

/// <summary>
/// Risposta di <see cref="UpdateRecordFunction"/>: il messaggio di indicizzazione se è cambiato il testo,
/// quello dei tag se è cambiato solo il titolo di un record pronto.
/// </summary>
public sealed class UpdateRecordOutput
{
    [HttpResult]
    public required IActionResult Result { get; init; }

    [QueueOutput("%Queues:Indexing%", Connection = "DataStorage")]
    public string? IndexingMessage { get; init; }

    [QueueOutput("%Queues:Tagging%", Connection = "DataStorage")]
    public string? TaggingMessage { get; init; }
}

/// <summary>
/// Risposta di <see cref="RetryRecordFunction"/>: al più uno dei messaggi, secondo il punto da cui
/// riparte l'elaborazione.
/// </summary>
public sealed class RetryOutput
{
    [HttpResult]
    public required IActionResult Result { get; init; }

    /// <summary>URL dell'originale, senza SAS (come in <see cref="ValidateUploadFunction"/>).</summary>
    [QueueOutput("%Queues:Normalization%", Connection = "DataStorage")]
    public string? NormalizationMessage { get; init; }

    [QueueOutput("%Queues:Transcription%", Connection = "DataStorage")]
    public string? TranscriptionMessage { get; init; }

    [QueueOutput("%Queues:Indexing%", Connection = "DataStorage")]
    public string? IndexingMessage { get; init; }
}
