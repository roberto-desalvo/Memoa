using 'main.bicep'

param environmentName = 'prod'

// Server SQL condiviso: nomi passati dall'ambiente per non pubblicarli nella repo.
param sqlServerResourceGroup = readEnvironmentVariable('SQL_SERVER_RESOURCE_GROUP')
param sqlServerName = readEnvironmentVariable('SQL_SERVER_NAME')

// dev + prod = 0,16 GB al giorno, cioè circa i 5 GB gratuiti al mese.
param logDailyCapGb = '0.1'

// Motore del job di trascrizione: 'whisper' o 'parakeet'. Il deploy imposta l'immagine del motore scelto;
// la pipeline del job poi la aggiorna solo con l'immagine dello stesso motore.
param transcriberEngine = 'whisper'

// Feature flag: con false le Functions non chiamano Azure AI Speech e ogni trascrizione va al job.
param useAzureSpeech = true

// Immagini segnaposto finché la pipeline del job non pubblica su GHCR. Dopo la prima pubblicazione
// impostare TRANSCRIBER_IMAGE_PROD (es. ghcr.io/<owner>/memoa-transcriber:prod) e
// TRANSCRIBER_PARAKEET_IMAGE_PROD (es. ghcr.io/<owner>/memoa-transcriber-parakeet:prod): il deploy
// dell'infrastruttura sostituisce l'immagine impostata dalla pipeline con quella del motore scelto.
param transcriberImage = readEnvironmentVariable('TRANSCRIBER_IMAGE_PROD', 'mcr.microsoft.com/k8se/quickstart-jobs:latest')
param transcriberParakeetImage = readEnvironmentVariable('TRANSCRIBER_PARAKEET_IMAGE_PROD', 'mcr.microsoft.com/k8se/quickstart-jobs:latest')
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
