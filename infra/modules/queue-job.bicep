// Container Apps Job attivato dalla propria coda tramite lo scaler KEDA (normalizzazione o trascrizione).
// Fatturato solo durante l'esecuzione, dentro il grant gratuito di Container Apps.
// Trasformazione da blob a blob: legge il messaggio dalla propria coda, legge il file di ingresso e
// scrive il risultato nel contenitore di destinazione; l'evento "blob creato" fa proseguire il flusso.
// Nessuna scrittura sulle code, nessun accesso ad Azure SQL.
param location string
param tags object
param baseName string

@description('Nome breve del job, usato nel nome della risorsa e del container (es. transcriber).')
param jobName string

param environmentId string
param identityId string
param identityClientId string

param image string
param cpu string
param memory string

@description('Utente GHCR; vuoto se l\'immagine è pubblica.')
param ghcrUsername string

@description('URI del segreto Key Vault con il token GHCR; vuoto se l\'immagine è pubblica.')
param ghcrTokenSecretUri string

param appInsightsConnectionString string
param dataStorageName string
param dataBlobEndpoint string
param dataQueueEndpoint string

@description('Coda da cui il job legge i messaggi e che ne determina l\'avvio.')
param queueName string

@description('Contenitore del file di ingresso.')
param inputContainerName string

@description('Contenitore in cui il job scrive il risultato.')
param outputContainerName string

@description('Durata massima di un\'esecuzione in secondi.')
param replicaTimeout int

@description('Esecuzioni contemporanee massime.')
param maxExecutions int = 2

var usePrivateRegistry = !empty(ghcrUsername) && !empty(ghcrTokenSecretUri)

resource job 'Microsoft.App/jobs@2025-01-01' = {
  name: 'caj-${baseName}-${jobName}'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Event'
      replicaTimeout: replicaTimeout
      // I retry sono gestiti dalla coda (messaggio di nuovo visibile, poi coda poison).
      replicaRetryLimit: 0
      eventTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
        scale: {
          minExecutions: 0
          maxExecutions: maxExecutions
          pollingInterval: 30
          rules: [
            {
              name: '${queueName}-queue'
              type: 'azure-queue'
              metadata: {
                accountName: dataStorageName
                queueName: queueName
                queueLength: '1'
              }
              identity: identityId
            }
          ]
        }
      }
      secrets: usePrivateRegistry
        ? [
            {
              name: 'ghcr-token'
              keyVaultUrl: ghcrTokenSecretUri
              identity: identityId
            }
          ]
        : []
      registries: usePrivateRegistry
        ? [
            {
              server: 'ghcr.io'
              username: ghcrUsername
              passwordSecretRef: 'ghcr-token'
            }
          ]
        : []
    }
    template: {
      containers: [
        {
          name: jobName
          image: image
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: [
            { name: 'AZURE_CLIENT_ID', value: identityClientId }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
            { name: 'DataStorage__BlobServiceUri', value: dataBlobEndpoint }
            { name: 'DataStorage__QueueServiceUri', value: dataQueueEndpoint }
            { name: 'Job__Queue', value: queueName }
            { name: 'Job__InputContainer', value: inputContainerName }
            { name: 'Job__OutputContainer', value: outputContainerName }
          ]
        }
      ]
    }
  }
}

output jobName string = job.name
