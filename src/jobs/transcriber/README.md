# Job di trascrizione

Ripiego della trascrizione: Container Apps Job (worker .NET 10 + ffmpeg + un motore di trascrizione su CPU) avviato dallo scaler KEDA sulla coda `transcription-fallback`. Ogni esecuzione elabora un messaggio e termina. Il motore è Whisper (Whisper.net), Parakeet (sherpa-onnx) oppure faster-whisper (script Python con CTranslate2), uno per immagine: vedi [Motore di trascrizione](#motore-di-trascrizione).

La trascrizione normale la fa la Function `TranscribeRecord` (coda `transcription`) con Azure AI Speech, e salva il testo direttamente nel database. Solo se il servizio non risponde entro il timeout, si superano i limiti (HTTP 429 o 413) o falliscono tutti i tentativi, la Function inoltra lo stesso messaggio su `transcription-fallback`, e il flusso passa da questo job e dal blob della trascrizione. Con Azure AI Speech disattivato (`useAzureSpeech = false` nei `.bicepparam`, cioè `Speech__Enabled=false` nelle Functions) ogni trascrizione passa da questo job.

1. Legge un messaggio dalla coda, invisibile per 65 minuti. Il messaggio è il JSON `{"blobUrl": "...", "fallbackLanguage": "it"}` in base64, scritto da `TranscribeRecord` (contratto in `src/shared/Pipeline/TranscriptionRequest.cs`).
2. Legge la durata dai metadati della copia normalizzata (`durationseconds`): oltre `Job__MaxDurationSeconds` scrive l'esito `audio_too_long` senza trascrivere. Il deploy la imposta in base al motore: 600 (10 minuti) con Whisper e faster-whisper, la durata massima dell'app (30 minuti) con Parakeet. Altrimenti scarica la copia da `audio/<oid>/<RecordUid>.opus` in una cartella temporanea.
3. `ffmpeg` la decodifica in PCM float32 mono a 16 kHz.
4. Rileva la lingua sui primi 30 secondi, scegliendo solo tra italiano e inglese (con Parakeet tramite Whisper tiny). Se la probabilità è sotto la soglia (`<Motore>__LanguageProbabilityThreshold`, predefinita 0,5) usa `fallbackLanguage`, la lingua dell'account.
5. Trascrive (vedi [Motore di trascrizione](#motore-di-trascrizione)) e scrive **uno** dei due blob nel contenitore `transcripts`, solo se nessuno dei due esiste già (scrittura condizionale):

| Blob | Quando | Metadati |
|---|---|---|
| `<oid>/<RecordUid>.txt` | Trascrizione riuscita (UTF-8) | `language` (lingua usata), `detectedlanguage` e `languageprobability` (esito del rilevamento), `engine` (`whisper.net/ggml-large-v3-turbo-q5_0`, `sherpa-onnx/parakeet-tdt-0.6b-v3-int8` o `faster-whisper/large-v3-turbo-int8`) |
| `<oid>/<RecordUid>.error.json` | Trascrizione scartata | `errorcode`; corpo `{"errorCode":"..."}` |

6. Elimina il messaggio. L'evento "blob creato" sul contenitore `transcripts` fa proseguire il flusso: la Function deve distinguere i due casi dal suffisso del nome (`Transcript.TryParse`).

Il job non scrive nel database né sulle code e legge solo `audio`. Nei log solo identificativi, lingua, durata dell'elaborazione e numero di caratteri: mai il testo.

## Codici d'errore

Definiti in `src/shared/Pipeline/Transcript.cs`. Ogni codice indica la fase non riuscita; l'eccezione completa è nei log del job (Application Insights) con lo stesso `RecordUid`.

| Codice | Quando | Cosa controllare |
|---|---|---|
| `no_speech` | Subito: trascrizione vuota, nessun parlato riconosciuto | L'audio (silenzio, rumore). Unico codice dovuto al contenuto |
| `audio_too_long` | Subito: registrazione oltre `Job__MaxDurationSeconds` | Perché Azure AI Speech non ha trascritto il record: log di `TranscribeRecord` con lo stesso `RecordUid` |
| `invalid_request` | Subito: il messaggio identifica il record ma la lingua di ripiego non è supportata | `Users.PreferredLanguage` e `TranscribeRecord` |
| `audio_download_failed` | All'ultimo tentativo: lettura della copia normalizzata non riuscita | Ruolo di lettura su `audio` dell'identità del job, rete, spazio su disco della replica |
| `audio_decode_failed` | Subito, senza retry: ffmpeg non decodifica la copia in PCM | ffmpeg nell'immagine (`Job__FfmpegPath`), copia corrotta |
| `model_load_failed` | Subito, senza retry: il motore non carica un modello | Riga di log "Caricamento del modello" (Whisper), "Caricamento dei modelli" (Parakeet) o "Avvio di faster-whisper": percorso, memoria disponibile; con faster-whisper l'eccezione riporta il tipo dell'errore Python. Poi i percorsi dei modelli (`Whisper__ModelPath`, `Parakeet__*`, `FasterWhisper__*`), checksum, memoria del job |
| `transcription_failed` | All'ultimo tentativo: errore del motore durante rilevamento della lingua o trascrizione | Runtime nativo di Whisper.net o sherpa-onnx, codice di uscita e tipo dell'eccezione dello script faster-whisper, memoria |
| `transcript_upload_failed` | All'ultimo tentativo: scrittura della trascrizione non riuscita | Ruolo di scrittura su `transcripts` dell'identità del job |
| `processing_timeout` | Subito, senza retry: esecuzione interrotta dal SIGTERM allo scadere del `replicaTimeout` | Durata dell'audio rispetto alle vCPU del job |
| `processing_failed` | Messaggio oltre `Job__MaxDequeueCount` tentativi (predefinito 3) senza causa registrata: l'ultima esecuzione è terminata senza poter scrivere l'esito (memoria esaurita, replica arrestata) | Cronologia delle esecuzioni del job in Container Apps |

Gli errori deterministici (`audio_decode_failed`, `model_load_failed`, `processing_timeout`) si ripeterebbero uguali con la stessa immagine e lo stesso audio: il job scrive subito l'esito ed elimina il messaggio. Dopo la correzione l'utente riprova dal record.

Gli altri errori, prima dell'ultimo tentativo, terminano l'esecuzione con codice 1 e un warning con il codice. Il messaggio resta in coda ma **invisibile** per il `Job__VisibilityTimeout` (65 minuti): portale, `az storage message peek` e lo scaler KEDA vedono solo i messaggi visibili, quindi la coda sembra vuota e il record resta "in elaborazione". `ApproximateMessagesCount` della coda invece lo conta. All'ultimo tentativo il job scrive l'esito con il codice della fase ed elimina il messaggio, al posto della coda dei messaggi non elaborabili, su cui il job non può scrivere.

Casi senza esito, con il solo log:
- copia normalizzata non più presente (record o account eliminati): scrivere l'esito lascerebbe un blob senza record;
- messaggio da cui non si ricava il record (JSON errato, campo mancante, URL fuori dallo storage dei dati o dal contenitore `audio`, nome del blob non valido): log di errore con l'id del messaggio.

## Motore di trascrizione

Il job dipende solo da `ITranscriptionEngine` (`Engine/`): riceve il file Opus e la lingua di ripiego e restituisce testo e lingua. Le implementazioni sono due, ciascuna nella propria immagine con i soli modelli che le servono; il motore lo sceglie `Job__Engine`, impostato dal target del `Dockerfile` (`Program.cs` registra il motore e ne valida solo le opzioni).

| | `WhisperEngine` (target `whisper`) | `ParakeetEngine` (target `parakeet`) | `FasterWhisperEngine` (target `faster-whisper`) |
|---|---|---|---|
| Immagine | `ghcr.io/<owner>/memoa-transcriber` | `ghcr.io/<owner>/memoa-transcriber-parakeet` | `ghcr.io/<owner>/memoa-transcriber-faster-whisper` |
| Libreria | Whisper.net (whisper.cpp) | sherpa-onnx (ONNX Runtime), più Whisper.net per la lingua | faster-whisper (CTranslate2) in Python, avviato come processo |
| Modelli | `ggml-large-v3-turbo-q5_0.bin` (circa 550 MB) | Parakeet TDT 0.6B v3 int8 (circa 670 MB), Silero VAD, Whisper `ggml-tiny.bin` (circa 75 MB) | large-v3-turbo CTranslate2 float16 (circa 1,6 GB), quantizzato int8 al caricamento |
| Lingua | Rilevata da large-v3-turbo | Rilevata da Whisper tiny: Parakeet non la restituisce | Rilevata da large-v3-turbo |
| Punteggiatura | Prompt iniziale per lingua (`Whisper__Prompts__<lingua>`) | Nativa, con maiuscole | Prompt iniziale per lingua (`FasterWhisper__Prompts__<lingua>`, stessi predefiniti) |
| Audio lungo | Finestre di 30 s di whisper.cpp | Segmenti di parlato della VAD, al più `Parakeet__MaxSpeechSeconds` (20 s): il modello ONNX usa l'attention completa | Finestre di 30 s; la VAD Silero inclusa salta i silenzi |
| Durata massima | 10 minuti | Durata massima dell'app (30 minuti) | 10 minuti |

I modelli sono scaricati dal `Dockerfile` da indirizzi fissati e verificati con lo SHA-256: Whisper da Hugging Face (`ggerganov/whisper.cpp`, revisione fissata), Parakeet e Silero VAD dalla release `asr-models` di sherpa-onnx. Per cambiarli vanno aggiornati URL e checksum. faster-whisper usa il modello di Hugging Face `deepdml/faster-whisper-large-v3-turbo-ct2` a una revisione fissata, ogni file verificato con lo SHA-256, e le dipendenze Python fissate in `FasterWhisper/requirements.txt`. Licenze: Whisper e faster-whisper MIT, Parakeet CC-BY-4.0, Silero VAD MIT.

`FasterWhisperEngine` decodifica l'audio con ffmpeg come gli altri motori e avvia `python -I -X utf8 FasterWhisper/transcribe.py` (pubblicato con il worker): richiesta in JSON su stdin (percorso del PCM, modello, thread, beam, lingue, soglia, prompt), risultato in JSON su stdout. Lo script esce con 3 se il modello non si carica (`model_load_failed`) e con 1 per ogni altro errore; su stderr scrive solo il tipo dell'eccezione, mai il testo.

Con Whisper e 4 vCPU (dev e prod) la trascrizione impiega circa 3 volte la durata dell'audio: per questo accetta solo registrazioni fino a 10 minuti, che restano entro il `replicaTimeout` di 60 minuti.

### Scegliere il motore

Nei `.bicepparam` dell'ambiente: `transcriberEngine = 'whisper'`, `'parakeet'` oppure `'faster-whisper'`, poi il deploy dell'infrastruttura. Il deploy imposta l'immagine del motore (`TRANSCRIBER_IMAGE_<ENV>`, `TRANSCRIBER_PARAKEET_IMAGE_<ENV>` o `TRANSCRIBER_FASTER_WHISPER_IMAGE_<ENV>`) e la durata massima (`Job__MaxDurationSeconds`). La pipeline del job pubblica sempre tutte le immagini, ma aggiorna il job solo con quella dello stesso repository dell'immagine in uso: non cambia il motore scelto.

## Configurazione

Variabili d'ambiente impostate da `infra/modules/queue-job.bicep`:

| Variabile | Esempio |
|---|---|
| `DataStorage__BlobServiceUri` | `https://<account>.blob.core.windows.net/` |
| `DataStorage__QueueServiceUri` | `https://<account>.queue.core.windows.net/` |
| `Job__Queue` | `transcription-fallback` |
| `Job__InputContainer` | `audio` |
| `Job__OutputContainer` | `transcripts` |
| `AZURE_CLIENT_ID` | Client ID dell'identità `id-memoa-<env>-transcriber` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Facoltativa: senza, i log restano sulla console |

Impostate dal `Dockerfile`: `Job__Engine` e i percorsi dei modelli (`Whisper__ModelPath`; `Parakeet__ModelDirectory`, `Parakeet__VadModelPath`, `Parakeet__LanguageModelPath`; `FasterWhisper__PythonPath`, `FasterWhisper__ModelDirectory`). Facoltative con Parakeet: `Parakeet__LanguageProbabilityThreshold`, `Parakeet__MaxSpeechSeconds`, `Parakeet__Threads`. Facoltative con faster-whisper: `FasterWhisper__ScriptPath` (predefinito accanto al worker), `FasterWhisper__LanguageProbabilityThreshold`, `FasterWhisper__BeamSize` (predefinito 1, ricerca greedy; 5, il predefinito di faster-whisper, è più lento), `FasterWhisper__Threads`, `FasterWhisper__Prompts__<lingua>`. Facoltative con Whisper e comuni: `Whisper__LanguageProbabilityThreshold`, `Whisper__Threads` (predefinito: le vCPU viste dal processo, che in Container Apps possono superare quelle del job), `Whisper__Prompts__<lingua>` (stringa vuota per trascrivere senza prompt), `Job__MaxDurationSeconds`, `Job__MaxDequeueCount`, `Job__VisibilityTimeout` (formato `hh:mm:ss`, deve superare il `replicaTimeout`), `Job__FfmpegPath`.

## Esecuzione in locale

Richiede ffmpeg nel `PATH`, i modelli del motore scaricati in locale e un login con `az login`; il proprio utente deve avere sullo storage dati dev gli stessi ruoli dell'identità del job (lettura di `audio`, scrittura su `transcripts`, Queue Data Message Processor su `transcription-fallback`). I runtime nativi per Windows sono inclusi nei pacchetti `Whisper.net.Runtime` e `org.k2fsa.sherpa.onnx`.

```powershell
$env:DataStorage__BlobServiceUri = "https://<storage-dati-dev>.blob.core.windows.net/"
$env:DataStorage__QueueServiceUri = "https://<storage-dati-dev>.queue.core.windows.net/"
$env:Job__Queue = "transcription-fallback"
$env:Job__InputContainer = "audio"
$env:Job__OutputContainer = "transcripts"
```

Con Whisper:

```powershell
Invoke-WebRequest https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin -OutFile $env:TEMP\ggml-large-v3-turbo-q5_0.bin
$env:Whisper__ModelPath = "$env:TEMP\ggml-large-v3-turbo-q5_0.bin"
dotnet run --project src/jobs/transcriber
```

Con Parakeet (l'archivio del modello si estrae con `tar`, incluso in Windows):

```powershell
Invoke-WebRequest https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2 -OutFile $env:TEMP\parakeet.tar.bz2
tar -xjf $env:TEMP\parakeet.tar.bz2 -C $env:TEMP
Invoke-WebRequest https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx -OutFile $env:TEMP\silero_vad.onnx
Invoke-WebRequest https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin -OutFile $env:TEMP\ggml-tiny.bin
$env:Job__Engine = "parakeet"
$env:Parakeet__ModelDirectory = "$env:TEMP\sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8"
$env:Parakeet__VadModelPath = "$env:TEMP\silero_vad.onnx"
$env:Parakeet__LanguageModelPath = "$env:TEMP\ggml-tiny.bin"
dotnet run --project src/jobs/transcriber
```

Con faster-whisper (Python 3.12 o successivo, in un virtualenv; il modello alla stessa revisione del `Dockerfile`):

```powershell
python -m venv $env:TEMP\fw
& $env:TEMP\fw\Scripts\python.exe -m pip install -r src/jobs/transcriber/FasterWhisper/requirements.txt
# In $env:TEMP\faster-whisper-large-v3-turbo: config.json, model.bin, preprocessor_config.json, tokenizer.json e
# vocabulary.json da https://huggingface.co/deepdml/faster-whisper-large-v3-turbo-ct2
$env:Job__Engine = "faster-whisper"
$env:FasterWhisper__PythonPath = "$env:TEMP\fw\Scripts\python.exe"
$env:FasterWhisper__ModelDirectory = "$env:TEMP\faster-whisper-large-v3-turbo"
dotnet run --project src/jobs/transcriber
```

Con Docker (il contesto è `src/`, perché l'immagine include anche `src/shared`; senza `--target` si costruisce Whisper):

```powershell
docker build -f src/jobs/transcriber/Dockerfile --target whisper -t memoa-transcriber src
docker build -f src/jobs/transcriber/Dockerfile --target parakeet -t memoa-transcriber-parakeet src
docker build -f src/jobs/transcriber/Dockerfile --target faster-whisper -t memoa-transcriber-faster-whisper src
```

## Immagine

La pipeline `.github/workflows/transcriber.yml` pubblica tutte le immagini, `ghcr.io/<owner>/memoa-transcriber` (Whisper), `ghcr.io/<owner>/memoa-transcriber-parakeet` e `ghcr.io/<owner>/memoa-transcriber-faster-whisper`, con i tag `<sha>` e `<branch>`, poi aggiorna il job dell'ambiente (`main` → dev, `prod` → prod) con il tag `<sha>` dell'immagine dello stesso motore già in uso. Se il job ha ancora l'immagine segnaposto non lo aggiorna. Il deploy dell'infrastruttura usa invece `TRANSCRIBER_IMAGE_<ENV>`, `TRANSCRIBER_PARAKEET_IMAGE_<ENV>` o `TRANSCRIBER_FASTER_WHISPER_IMAGE_<ENV>` (il tag del branch), secondo `transcriberEngine`: vedi [infra/README.md](../../../infra/README.md#deploy).
