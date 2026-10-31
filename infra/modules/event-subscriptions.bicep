// Sottoscrizioni Event Grid "blob creato" → Functions, una per contenitore (filtro sul prefisso del soggetto).
// Ogni passaggio scrive in un contenitore diverso da quello che legge, quindi non ci sono cicli.
// L'endpoint AzureFunction richiede che le funzioni esistano già: il modulo va distribuito
// solo dopo il primo deploy del codice delle Functions (vedi infra/README.md).
// Nessun dead-letter per ora: gli eventi non consegnati dopo i retry automatici vanno persi.
param eventTopicName string
param functionAppName string
param uploadsContainerName string
param audioContainerName string
param transcriptsContainerName string

@description('Nomi delle funzioni con trigger Event Grid, come definiti nel codice. Nome vuoto = sottoscrizione non creata (funzione non ancora implementata).')
param functionNames object = {
  validateUpload: 'ValidateUpload'
  enqueueTranscription: 'EnqueueTranscription'
  // Da impostare a 'EnqueueIndexing' quando la funzione esiste nel codice distribuito.
  enqueueIndexing: ''
}

resource eventTopic 'Microsoft.EventGrid/systemTopics@2025-02-15' existing = {
  name: eventTopicName
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: functionAppName
}

var subscriptions = filter([
  {
    name: 'uploads-to-validation'
    container: uploadsContainerName
    functionName: functionNames.validateUpload
  }
  {
    name: 'audio-to-transcription'
    container: audioContainerName
    functionName: functionNames.enqueueTranscription
  }
  {
    name: 'transcripts-to-indexing'
    container: transcriptsContainerName
    functionName: functionNames.enqueueIndexing
  }
], subscription => !empty(subscription.functionName))

resource eventSubscriptions 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2025-02-15' = [
  for subscription in subscriptions: {
    parent: eventTopic
    name: subscription.name
    properties: {
      destination: {
        endpointType: 'AzureFunction'
        properties: {
          resourceId: '${functionApp.id}/functions/${subscription.functionName}'
          maxEventsPerBatch: 1
        }
      }
      eventDeliverySchema: 'EventGridSchema'
      filter: {
        includedEventTypes: [
          'Microsoft.Storage.BlobCreated'
        ]
        subjectBeginsWith: '/blobServices/default/containers/${subscription.container}/'
      }
      // Retry automatici di Event Grid (valori predefiniti), consegna almeno una volta:
      // le funzioni vanno rese idempotenti.
      retryPolicy: {
        maxDeliveryAttempts: 30
        eventTimeToLiveInMinutes: 1440
      }
    }
  }
]
