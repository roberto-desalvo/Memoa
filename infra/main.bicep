// Memoa – infrastruttura di un ambiente (dev o prod).
// Deploy: az deployment sub create -l westeurope -f infra/main.bicep -p infra/main.<env>.bicepparam
targetScope = 'subscription'

@description('Nome dell\'ambiente.')
@allowed([
  'dev'
  'prod'
])
param environmentName string

@description('Regione di tutte le risorse. Deve coincidere con quella dei database SQL gratuiti già esistenti nella sottoscrizione.')
param location string = 'westeurope'

@description('Nome del progetto, usato come prefisso dei nomi delle risorse.')
param projectName string = 'memoa'

@description('Resource group del server SQL esistente, condiviso con altri progetti.')
param sqlServerResourceGroup string

@description('Nome del server SQL esistente su cui creare il database.')
param sqlServerName string

@description('Limite giornaliero di ingestione di Log Analytics in GB. La somma dei due ambienti va tenuta entro i 5 GB gratuiti al mese.')
param logDailyCapGb string

@description('Motore del job di trascrizione: sceglie quale delle due immagini usare.')
@allowed([
  'whisper'
  'parakeet'
  'faster-whisper'
])
param transcriberEngine string = 'whisper'

@description('Immagine del job di trascrizione con Whisper (es. ghcr.io/<owner>/memoa-transcriber:<tag>).')
param transcriberImage string

@description('Immagine del job di trascrizione con Parakeet (es. ghcr.io/<owner>/memoa-transcriber-parakeet:<tag>).')
param transcriberParakeetImage string

@description('Immagine del job di trascrizione con faster-whisper (es. ghcr.io/<owner>/memoa-transcriber-faster-whisper:<tag>).')
param transcriberFasterWhisperImage string

@description('Feature flag: con false le Functions non chiamano Azure AI Speech e ogni trascrizione va al job.')
param useAzureSpeech bool = true

@description('Solo con faster-whisper: segmenti trascritti insieme. 1 è la trascrizione sequenziale, con il testo precedente come contesto.')
@minValue(1)
@maxValue(32)
param fasterWhisperBatchSize int = 1

@description('Utente GitHub proprietario del token per GHCR. Vuoto se le immagini sono pubbliche.')
param ghcrUsername string = ''

@description('Personal access token GitHub con scope read:packages. Vuoto se le immagini sono pubbliche.')
@secure()
param ghcrToken string = ''

@description('vCPU del job di trascrizione.')
param transcriberCpu string = '4.0'

@description('Memoria del job di trascrizione.')
param transcriberMemory string = '8Gi'

@description('Giorni dopo i quali gli audio originali passano al tier Cold.')
param uploadsColdAfterDays int = 1

@description('Giorni dopo i quali le copie normalizzate passano al tier Cool.')
param audioCoolAfterDays int = 30

@description('Durata massima di un audio in minuti, per la registrazione e per i file caricati: unico punto in cui si imposta (Functions e, tramite la pipeline, config.json del frontend).')
@minValue(1)
param maxAudioDurationMinutes int = 30

@description('Crea le sottoscrizioni Event Grid verso le Functions. Richiede che il codice delle Functions sia già distribuito.')
param deployEventSubscriptions bool = false

@description('Authority del tenant Entra External ID (https://<sottodominio>.ciamlogin.com/<tenant-id>/v2.0).')
param authAuthority string

@description('Client ID della registrazione dell\'API in Entra External ID per questo ambiente.')
param authAudience string

@description('Origini aggiuntive consentite da CORS (es. http://localhost:4200 in dev).')
param extraAllowedOrigins array = []

@description('Repository GitHub (<owner>/<repo>) le cui pipeline distribuiscono su questo ambiente.')
param githubRepository string

@description('Object ID degli utenti sviluppatori che eseguono le Functions in locale contro questo ambiente (solo dev).')
param developerPrincipalIds array = []

var tags = {
  project: projectName
  environment: environmentName
}

// Immagine del job di trascrizione per ogni motore.
var transcriberImages = {
  whisper: transcriberImage
  parakeet: transcriberParakeetImage
  'faster-whisper': transcriberFasterWhisperImage
}

// Variabile d'ambiente letta dai .bicepparam per ogni motore, per il messaggio d'errore.
var transcriberImageVariables = {
  whisper: 'TRANSCRIBER_IMAGE_${toUpper(environmentName)}'
  parakeet: 'TRANSCRIBER_PARAKEET_IMAGE_${toUpper(environmentName)}'
  'faster-whisper': 'TRANSCRIBER_FASTER_WHISPER_IMAGE_${toUpper(environmentName)}'
}

// Senza l'immagine del motore scelto il deploy si ferma in validazione, prima di modificare le risorse:
// nessun ripiego silenzioso su un'immagine che non trascrive.
var transcriberSelectedImage = empty(transcriberImages[transcriberEngine])
  ? fail('Nessuna immagine per il motore di trascrizione \'${transcriberEngine}\': impostare ${transcriberImageVariables[transcriberEngine]} (vedi infra/README.md)')
  : transcriberImages[transcriberEngine]

// Suffisso stabile per i nomi globalmente univoci.
var suffix = take(uniqueString(subscription().id, projectName, environmentName), 5)
var baseName = '${projectName}-${environmentName}'
var compactName = '${projectName}${environmentName}${suffix}'

// Nomi delle identità noti all'avvio: i moduli li usano per i nomi delle assegnazioni RBAC.
var functionIdentityName = 'id-${baseName}-func'
var transcriberIdentityName = 'id-${baseName}-transcriber'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${baseName}'
  location: location
  tags: tags
}

module identities 'modules/identities.bicep' = {
  scope: rg
  name: 'identities'
  params: {
    location: location
    tags: tags
    functionIdentityName: functionIdentityName
    transcriberIdentityName: transcriberIdentityName
  }
}

module monitoring 'modules/monitoring.bicep' = {
  scope: rg
  name: 'monitoring'
  params: {
    location: location
    tags: tags
    baseName: baseName
    dailyCapGb: logDailyCapGb
    publisherIdentityNames: [
      functionIdentityName
      transcriberIdentityName
    ]
  }
  dependsOn: [
    identities
  ]
}

// Query salvate per il monitoraggio (testi in modules/log-queries/*.kql).
module logQueries 'modules/log-queries.bicep' = {
  scope: rg
  name: 'logQueries'
  params: {
    workspaceName: monitoring.outputs.logAnalyticsWorkspaceName
    dailyCapGb: logDailyCapGb
  }
}

// Workbook di monitoraggio con le stesse query, una scheda per categoria.
module monitoringWorkbook 'modules/monitoring-workbook.bicep' = {
  scope: rg
  name: 'monitoringWorkbook'
  params: {
    location: location
    tags: tags
    environmentName: environmentName
    workspaceName: monitoring.outputs.logAnalyticsWorkspaceName
    dailyCapGb: logDailyCapGb
  }
}

module staticWebApp 'modules/static-web-app.bicep' = {
  scope: rg
  name: 'staticWebApp'
  params: {
    location: location
    tags: tags
    baseName: baseName
  }
}

var allowedOrigins = concat([ 'https://${staticWebApp.outputs.defaultHostname}' ], extraAllowedOrigins)

module dataStorage 'modules/data-storage.bicep' = {
  scope: rg
  name: 'dataStorage'
  params: {
    location: location
    tags: tags
    name: take('st${compactName}data', 24)
    allowedOrigins: allowedOrigins
    uploadsColdAfterDays: uploadsColdAfterDays
    audioCoolAfterDays: audioCoolAfterDays
    functionIdentityName: functionIdentityName
    transcriberIdentityName: transcriberIdentityName
    developerPrincipalIds: developerPrincipalIds
  }
  dependsOn: [
    identities
  ]
}

module keyVault 'modules/key-vault.bicep' = {
  scope: rg
  name: 'keyVault'
  params: {
    location: location
    tags: tags
    name: take('kv-${compactName}', 24)
    enablePurgeProtection: environmentName == 'prod'
    ghcrToken: ghcrToken
    // Solo il job: il token GHCR serve per il pull dell'immagine privata.
    secretReaderIdentityNames: [
      transcriberIdentityName
    ]
  }
  dependsOn: [
    identities
  ]
}

module sql 'modules/sql.bicep' = {
  scope: resourceGroup(sqlServerResourceGroup)
  name: 'sql-${baseName}'
  params: {
    location: location
    tags: tags
    serverName: sqlServerName
    databaseName: 'sqldb-${baseName}'
  }
}

module openAi 'modules/openai.bicep' = {
  scope: rg
  name: 'openAi'
  params: {
    location: location
    tags: tags
    name: 'oai-${baseName}-${suffix}'
    userIdentityNames: [
      functionIdentityName
    ]
    developerPrincipalIds: developerPrincipalIds
  }
  dependsOn: [
    identities
  ]
}

// Trascrizione chiamata dalla Function; il job con Whisper resta come ripiego.
module speech 'modules/speech.bicep' = {
  scope: rg
  name: 'speech'
  params: {
    location: location
    tags: tags
    name: 'spch-${baseName}-${suffix}'
    userIdentityNames: [
      functionIdentityName
    ]
    developerPrincipalIds: developerPrincipalIds
  }
  dependsOn: [
    identities
  ]
}

module functionApp 'modules/function-app.bicep' = {
  scope: rg
  name: 'functionApp'
  params: {
    location: location
    tags: tags
    baseName: baseName
    hostStorageName: take('st${compactName}func', 24)
    identityName: functionIdentityName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    allowedOrigins: allowedOrigins
    dataBlobEndpoint: dataStorage.outputs.blobEndpoint
    dataQueueEndpoint: dataStorage.outputs.queueEndpoint
    uploadsContainerName: dataStorage.outputs.uploadsContainerName
    audioContainerName: dataStorage.outputs.audioContainerName
    transcriptsContainerName: dataStorage.outputs.transcriptsContainerName
    normalizationQueueName: dataStorage.outputs.normalizationQueueName
    transcriptionQueueName: dataStorage.outputs.transcriptionQueueName
    transcriptionFallbackQueueName: dataStorage.outputs.transcriptionFallbackQueueName
    indexingQueueName: dataStorage.outputs.indexingQueueName
    taggingQueueName: dataStorage.outputs.taggingQueueName
    uploadExpiryQueueName: dataStorage.outputs.uploadExpiryQueueName
    sqlConnectionString: sql.outputs.managedIdentityConnectionStringPrefix
    openAiEndpoint: openAi.outputs.endpoint
    embeddingDeploymentName: openAi.outputs.embeddingDeploymentName
    speechEndpoint: speech.outputs.endpoint
    speechEnabled: useAzureSpeech
    maxAudioDurationMinutes: maxAudioDurationMinutes
    authAuthority: authAuthority
    authAudience: authAudience
  }
}

// Evento "blob creato" per contenitore → Function. Solo dopo il primo deploy del codice delle Functions.
module eventSubscriptions 'modules/event-subscriptions.bicep' = if (deployEventSubscriptions) {
  scope: rg
  name: 'eventSubscriptions'
  params: {
    eventTopicName: dataStorage.outputs.eventTopicName
    functionAppName: functionApp.outputs.name
    uploadsContainerName: dataStorage.outputs.uploadsContainerName
    audioContainerName: dataStorage.outputs.audioContainerName
    transcriptsContainerName: dataStorage.outputs.transcriptsContainerName
  }
}

// Identità delle pipeline GitHub Actions (environment GitHub con lo stesso nome dell'ambiente).
module deployIdentity 'modules/deploy-identity.bicep' = {
  scope: rg
  name: 'deployIdentity'
  params: {
    location: location
    tags: tags
    identityName: 'id-${baseName}-deploy'
    githubRepository: githubRepository
    githubEnvironment: environmentName
    functionAppName: functionApp.outputs.name
    staticWebAppName: staticWebApp.outputs.name
    // Pipeline dell'immagine del job.
    containerAppJobNames: [
      transcriber.outputs.jobName
    ]
  }
}

module containerAppsEnv 'modules/container-apps-env.bicep' = {
  scope: rg
  name: 'containerAppsEnv'
  params: {
    location: location
    tags: tags
    baseName: baseName
    logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
  }
}

// Ripiego della trascrizione: coda di ripiego (scritta dalla Function se Azure AI Speech non risponde o è
// disattivato) → Whisper, Parakeet o faster-whisper sulla copia normalizzata → file di testo nel contenitore delle
// trascrizioni. Il motore è nell'immagine: transcriberEngine sceglie quale usare.
module transcriber 'modules/queue-job.bicep' = {
  scope: rg
  name: 'transcriber'
  params: {
    location: location
    tags: tags
    baseName: baseName
    jobName: 'transcriber'
    environmentId: containerAppsEnv.outputs.id
    identityId: identities.outputs.transcriberIdentityId
    identityClientId: identities.outputs.transcriberClientId
    image: transcriberSelectedImage
    cpu: transcriberCpu
    memory: transcriberMemory
    ghcrUsername: ghcrUsername
    ghcrTokenSecretUri: keyVault.outputs.ghcrTokenSecretUri
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    dataStorageName: dataStorage.outputs.name
    dataBlobEndpoint: dataStorage.outputs.blobEndpoint
    dataQueueEndpoint: dataStorage.outputs.queueEndpoint
    queueName: dataStorage.outputs.transcriptionFallbackQueueName
    inputContainerName: dataStorage.outputs.audioContainerName
    outputContainerName: dataStorage.outputs.transcriptsContainerName
    // Whisper e faster-whisper impiegano più del tempo reale: solo registrazioni fino a 10 minuti.
    // Parakeet, molto più veloce, accetta la durata massima dell'app.
    env: [
      {
        name: 'Job__MaxDurationSeconds'
        value: string(transcriberEngine == 'parakeet' ? maxAudioDurationMinutes * 60 : 600)
      }
      // Ignorata dagli altri motori.
      {
        name: 'FasterWhisper__BatchSize'
        value: string(fasterWhisperBatchSize)
      }
    ]
    // 10 minuti di audio con Whisper o faster-whisper, con margine. Deve restare sotto Job__VisibilityTimeout del worker
    // (65 minuti).
    replicaTimeout: 3600
  }
}

output resourceGroupName string = rg.name
output functionAppName string = functionApp.outputs.name
output functionAppHostname string = functionApp.outputs.defaultHostname
output staticWebAppName string = staticWebApp.outputs.name
output staticWebAppHostname string = staticWebApp.outputs.defaultHostname
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output transcriberJobName string = transcriber.outputs.jobName
output functionIdentityName string = identities.outputs.functionIdentityName
output transcriberIdentityName string = identities.outputs.transcriberIdentityName
output deployIdentityName string = deployIdentity.outputs.name
output deployIdentityClientId string = deployIdentity.outputs.clientId
