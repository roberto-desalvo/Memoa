using 'main.bicep'

param environmentName = 'dev'

// Server SQL condiviso: nomi passati dall'ambiente per non pubblicarli nella repo.
param sqlServerResourceGroup = readEnvironmentVariable('SQL_SERVER_RESOURCE_GROUP')
param sqlServerName = readEnvironmentVariable('SQL_SERVER_NAME')

// dev + prod = 0,16 GB al giorno, cioè circa i 5 GB gratuiti al mese.
param logDailyCapGb = '0.06'

// Immagini segnaposto finché le pipeline dei job non pubblicano su GHCR.
param normalizerImage = 'mcr.microsoft.com/k8se/quickstart-jobs:latest'
param transcriberImage = 'mcr.microsoft.com/k8se/quickstart-jobs:latest'
param ghcrUsername = ''
param ghcrToken = readEnvironmentVariable('GHCR_TOKEN', '')

// Entra External ID: tenant esterno e registrazione dell'API distinti per ambiente.
param authAuthority = readEnvironmentVariable('AUTH_AUTHORITY_DEV')
param authAudience = readEnvironmentVariable('AUTH_AUDIENCE_DEV')

param normalizerCpu = '1.0'
param normalizerMemory = '2Gi'

param transcriberCpu = '2.0'
param transcriberMemory = '4Gi'

param extraAllowedOrigins = [
  'http://localhost:4200'
]

// Impostare a true dopo il primo deploy del codice delle Functions (vedi infra/README.md).
param deployEventSubscriptions = false
