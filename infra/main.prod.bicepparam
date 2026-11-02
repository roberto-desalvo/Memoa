using 'main.bicep'

param environmentName = 'prod'

// Server SQL condiviso: nomi passati dall'ambiente per non pubblicarli nella repo.
param sqlServerResourceGroup = readEnvironmentVariable('SQL_SERVER_RESOURCE_GROUP')
param sqlServerName = readEnvironmentVariable('SQL_SERVER_NAME')

// dev + prod = 0,16 GB al giorno, cioè circa i 5 GB gratuiti al mese.
param logDailyCapGb = '0.1'

// Motore del job di trascrizione: 'whisper', 'parakeet' o 'faster-whisper'. Il deploy imposta l'immagine del motore scelto;
// la pipeline del job poi la aggiorna solo con l'immagine dello stesso motore.
param transcriberEngine = 'whisper'

// Feature flag: con false le Functions non chiamano Azure AI Speech e ogni trascrizione va al job.
param useAzureSpeech = true

// Solo con faster-whisper: segmenti trascritti insieme (1 = sequenziale). Con 4 vCPU la prova locale ha dato
// il 28% in meno con 4, ma senza il contesto del testo precedente tra i segmenti.
param fasterWhisperBatchSize = 1

// Immagini del job per motore, dopo la pubblicazione su GHCR: TRANSCRIBER_IMAGE_PROD
// (ghcr.io/<owner>/memoa-transcriber:prod), TRANSCRIBER_PARAKEET_IMAGE_PROD (-parakeet:prod) e
// TRANSCRIBER_FASTER_WHISPER_IMAGE_PROD (-faster-whisper:prod). Quella del motore scelto è obbligatoria: se è
// vuota il deploy si ferma. Le altre possono restare vuote. Solo per il primo deploy, prima che la pipeline
// pubblichi le immagini, si può impostare esplicitamente mcr.microsoft.com/k8se/quickstart-jobs:latest.
param transcriberImage = readEnvironmentVariable('TRANSCRIBER_IMAGE_PROD', '')
param transcriberParakeetImage = readEnvironmentVariable('TRANSCRIBER_PARAKEET_IMAGE_PROD', '')
param transcriberFasterWhisperImage = readEnvironmentVariable('TRANSCRIBER_FASTER_WHISPER_IMAGE_PROD', '')
param ghcrUsername = 'roberto-desalvo'
param ghcrToken = readEnvironmentVariable('GHCR_TOKEN', '')

// Entra External ID: tenant esterno e registrazione dell'API distinti per ambiente.
param authAuthority = readEnvironmentVariable('AUTH_AUTHORITY_PROD')
param authAudience = readEnvironmentVariable('AUTH_AUDIENCE_PROD')

// Pipeline GitHub Actions: federated credential sull'environment GitHub 'prod'.
param githubRepository = 'roberto-desalvo/Memoa'

param transcriberCpu = '4.0'
param transcriberMemory = '8Gi'

// Impostare a true dopo il primo deploy del codice delle Functions (vedi infra/README.md).
param deployEventSubscriptions = false
