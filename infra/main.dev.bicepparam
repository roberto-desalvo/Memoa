using 'main.bicep'

param environmentName = 'dev'

// Server SQL condiviso: nomi passati dall'ambiente per non pubblicarli nella repo.
param sqlServerResourceGroup = readEnvironmentVariable('SQL_SERVER_RESOURCE_GROUP')
param sqlServerName = readEnvironmentVariable('SQL_SERVER_NAME')

// dev + prod = 0,16 GB al giorno, cioè circa i 5 GB gratuiti al mese.
param logDailyCapGb = '0.06'

// Motore del job di trascrizione: 'whisper', 'parakeet' o 'faster-whisper'. Il deploy imposta l'immagine del motore scelto;
// la pipeline del job poi la aggiorna solo con l'immagine dello stesso motore.
param transcriberEngine = 'faster-whisper'

// Feature flag: con false le Functions non chiamano Azure AI Speech e ogni trascrizione va al job.
param useAzureSpeech = false

// Solo con faster-whisper: segmenti trascritti insieme (1 = sequenziale). Con 4 vCPU la prova locale ha dato
// il 28% in meno con 4, ma senza il contesto del testo precedente tra i segmenti.
param fasterWhisperBatchSize = 1

// Immagini del job per motore, dopo la pubblicazione su GHCR: TRANSCRIBER_IMAGE_DEV
// (ghcr.io/<owner>/memoa-transcriber:main), TRANSCRIBER_PARAKEET_IMAGE_DEV (-parakeet:main) e
// TRANSCRIBER_FASTER_WHISPER_IMAGE_DEV (-faster-whisper:main). Quella del motore scelto è obbligatoria: se è
// vuota il deploy si ferma. Le altre possono restare vuote. Solo per il primo deploy, prima che la pipeline
// pubblichi le immagini, si può impostare esplicitamente mcr.microsoft.com/k8se/quickstart-jobs:latest.
param transcriberImage = readEnvironmentVariable('TRANSCRIBER_IMAGE_DEV', '')
param transcriberParakeetImage = readEnvironmentVariable('TRANSCRIBER_PARAKEET_IMAGE_DEV', '')
param transcriberFasterWhisperImage = readEnvironmentVariable('TRANSCRIBER_FASTER_WHISPER_IMAGE_DEV', '')
param ghcrUsername = 'roberto-desalvo'
param ghcrToken = readEnvironmentVariable('GHCR_TOKEN', '')

// Entra External ID: tenant esterno e registrazione dell'API distinti per ambiente.
param authAuthority = readEnvironmentVariable('AUTH_AUTHORITY_DEV')
param authAudience = readEnvironmentVariable('AUTH_AUDIENCE_DEV')

// Pipeline GitHub Actions: federated credential sull'environment GitHub 'dev'.
param githubRepository = 'roberto-desalvo/Memoa'

param transcriberCpu = '4.0'
param transcriberMemory = '8Gi'

param extraAllowedOrigins = [
  'http://localhost:4200'
]

// Utenti che eseguono le Functions in locale contro dev: ricevono gli stessi ruoli dati delle Functions.
param developerPrincipalIds = filter(split(readEnvironmentVariable('DEVELOPER_PRINCIPAL_IDS_DEV', ''), ','), id => !empty(trim(id)))

// Impostare a true dopo il primo deploy del codice delle Functions (vedi infra/README.md).
param deployEventSubscriptions = true
