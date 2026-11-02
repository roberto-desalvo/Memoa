using System.ComponentModel.DataAnnotations;

namespace Memoa.Transcriber;

/// <summary>
/// Endpoint dello storage account dei dati utente (sezione "DataStorage"). L'autenticazione è sempre
/// Entra ID: in Azure l'identità user-assigned indicata da AZURE_CLIENT_ID, in locale l'utente di
/// Azure CLI o Visual Studio.
/// </summary>
public sealed class DataStorageOptions
{
    public const string SectionName = "DataStorage";

    /// <summary>Es. https://&lt;account&gt;.blob.core.windows.net/.</summary>
    [Required]
    public Uri? BlobServiceUri { get; set; }

    /// <summary>Es. https://&lt;account&gt;.queue.core.windows.net/.</summary>
    [Required]
    public Uri? QueueServiceUri { get; set; }
}

/// <summary>Configurazione del job (sezione "Job", impostata da infra/modules/queue-job.bicep).</summary>
public sealed class JobOptions
{
    public const string SectionName = "Job";

    /// <summary>
    /// Coda di ripiego della trascrizione, scritta dalla Function quando Azure AI Speech non risponde: l'unica
    /// letta dal job.
    /// </summary>
    [Required]
    public string Queue { get; set; } = string.Empty;

    /// <summary>
    /// Motore di trascrizione, impostato dal target del Dockerfile: ogni immagine contiene solo i modelli
    /// del proprio motore.
    /// </summary>
    [AllowedValues(WhisperEngineName, ParakeetEngineName, FasterWhisperEngineName)]
    public string Engine { get; set; } = WhisperEngineName;

    public const string WhisperEngineName = "whisper";
    public const string ParakeetEngineName = "parakeet";
    public const string FasterWhisperEngineName = "faster-whisper";

    /// <summary>
    /// Durata massima accettata, in secondi: le registrazioni più lunghe vengono rifiutate con l'esito
    /// "audio_too_long" senza trascriverle (Whisper su CPU impiega circa 3 volte il tempo reale).
    /// </summary>
    [Range(1, 30 * 60)]
    public int MaxDurationSeconds { get; set; } = 10 * 60;

    /// <summary>Contenitore dell'audio normalizzato (sola lettura).</summary>
    [Required]
    public string InputContainer { get; set; } = string.Empty;

    /// <summary>Contenitore delle trascrizioni, l'unico in cui il job scrive.</summary>
    [Required]
    public string OutputContainer { get; set; } = string.Empty;

    /// <summary>
    /// Tentativi oltre i quali il messaggio viene scartato con l'esito "processing_failed": il job non
    /// può scrivere sulla coda dei messaggi non elaborabili.
    /// </summary>
    [Range(1, 10)]
    public int MaxDequeueCount { get; set; } = 3;

    /// <summary>
    /// Deve superare la durata massima di un'esecuzione (replicaTimeout, 60 minuti), altrimenti il
    /// messaggio torna visibile mentre è ancora in elaborazione.
    /// </summary>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(65);

    public string FfmpegPath { get; set; } = "ffmpeg";
}

/// <summary>Configurazione del motore Whisper (sezione "Whisper").</summary>
public sealed class WhisperOptions
{
    public const string SectionName = "Whisper";

    /// <summary>File GGML del modello, incluso nell'immagine (impostato dal Dockerfile).</summary>
    [Required]
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Probabilità minima della lingua rilevata; sotto questa soglia si usa la lingua dell'account.
    /// Da tarare sulle registrazioni reali.
    /// </summary>
    [Range(0.0, 1.0)]
    public double LanguageProbabilityThreshold { get; set; } = 0.5;

    /// <summary>
    /// Thread di whisper.cpp; se assente, Environment.ProcessorCount. In Container Apps
    /// ProcessorCount può superare le vCPU assegnate al job: più thread delle vCPU rallentano
    /// l'inferenza.
    /// </summary>
    [Range(1, 64)]
    public int? Threads { get; set; }

    /// <summary>
    /// Prompt iniziale per lingua: Whisper imita lo stile del testo che lo precede, quindi un esempio
    /// punteggiato lo spinge a usare la punteggiatura. Va tenuto breve e neutro: con audio silenzioso o
    /// rumoroso Whisper può ripeterne dei frammenti. Una lingua senza prompt (o con prompt vuoto) si
    /// trascrive senza.
    /// </summary>
    public Dictionary<string, string> Prompts { get; set; } = DefaultPrompts();

    /// <summary>Prompt predefiniti, condivisi con faster-whisper.</summary>
    public static Dictionary<string, string> DefaultPrompts() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["it"] = "Allora... oggi è stata una giornata lunga, ma alla fine è andata bene! E domani, invece, cosa farò? Vedremo.",
        ["en"] = "So... today was a long day, but in the end it went well! And tomorrow, though, what will I do? We'll see.",
    };
}

/// <summary>
/// Configurazione del motore faster-whisper (sezione "FasterWhisper"): script Python con CTranslate2,
/// percorsi impostati dal Dockerfile.
/// </summary>
public sealed class FasterWhisperOptions
{
    public const string SectionName = "FasterWhisper";

    /// <summary>Interprete Python con faster-whisper installato (nell'immagine, il virtualenv).</summary>
    [Required]
    public string PythonPath { get; set; } = "python3";

    /// <summary>Script di trascrizione, pubblicato con il worker.</summary>
    [Required]
    public string ScriptPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "FasterWhisper", "transcribe.py");

    /// <summary>Cartella del modello in formato CTranslate2 (large-v3-turbo).</summary>
    [Required]
    public string ModelDirectory { get; set; } = string.Empty;

    /// <summary>Probabilità minima della lingua rilevata; sotto questa soglia si usa la lingua dell'account.</summary>
    [Range(0.0, 1.0)]
    public double LanguageProbabilityThreshold { get; set; } = 0.5;

    /// <summary>
    /// Ampiezza della beam search: 1 è la ricerca greedy, come Whisper.net, la più veloce; il predefinito
    /// di faster-whisper è 5, più preciso ma più lento.
    /// </summary>
    [Range(1, 10)]
    public int BeamSize { get; set; } = 1;

    /// <summary>
    /// Segmenti della VAD trascritti insieme (BatchedInferencePipeline): 1 è la trascrizione sequenziale,
    /// in cui ogni finestra ha il testo precedente come contesto. Con valori maggiori i segmenti sono
    /// indipendenti e la memoria cresce con il batch.
    /// </summary>
    [Range(1, 32)]
    public int BatchSize { get; set; } = 1;

    /// <summary>Thread di CTranslate2; se assente, Environment.ProcessorCount.</summary>
    [Range(1, 64)]
    public int? Threads { get; set; }

    /// <summary>Prompt iniziale per lingua, come <see cref="WhisperOptions.Prompts"/>.</summary>
    public Dictionary<string, string> Prompts { get; set; } = WhisperOptions.DefaultPrompts();
}

/// <summary>Configurazione del motore Parakeet (sezione "Parakeet", percorsi impostati dal Dockerfile).</summary>
public sealed class ParakeetOptions
{
    public const string SectionName = "Parakeet";

    /// <summary>
    /// Cartella del modello sherpa-onnx Parakeet TDT 0.6B v3 int8: encoder.int8.onnx, decoder.int8.onnx,
    /// joiner.int8.onnx e tokens.txt.
    /// </summary>
    [Required]
    public string ModelDirectory { get; set; } = string.Empty;

    /// <summary>Modello Silero VAD (ONNX), che divide l'audio in segmenti di parlato.</summary>
    [Required]
    public string VadModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Modello GGML di Whisper per il rilevamento della lingua (tiny): Parakeet non restituisce la lingua
    /// trascritta.
    /// </summary>
    [Required]
    public string LanguageModelPath { get; set; } = string.Empty;

    /// <summary>Probabilità minima della lingua rilevata; sotto questa soglia si usa la lingua dell'account.</summary>
    [Range(0.0, 1.0)]
    public double LanguageProbabilityThreshold { get; set; } = 0.5;

    /// <summary>
    /// Durata massima di un segmento di parlato, in secondi: oltre, la VAD lo spezza anche senza pausa.
    /// Segmenti più lunghi danno più contesto al modello ma richiedono più memoria.
    /// </summary>
    [Range(5, 60)]
    public float MaxSpeechSeconds { get; set; } = 20;

    /// <summary>Thread di ONNX Runtime e di whisper.cpp; se assente, Environment.ProcessorCount.</summary>
    [Range(1, 64)]
    public int? Threads { get; set; }
}
