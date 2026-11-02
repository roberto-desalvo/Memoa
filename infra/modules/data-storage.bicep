// Storage account dedicato ai dati utente: blob (audio, file di testo, trascrizioni) e code di elaborazione.
// Separato da quello di sistema delle Functions per isolare accessi, lifecycle e cancellazioni GDPR.
// Emette gli eventi "blob creato" tramite un system topic Event Grid (sottoscrizioni in event-subscriptions.bicep).
param location string
param tags object
param name string

@description('Origini autorizzate a caricare file direttamente con token SAS.')
param allowedOrigins array

@description('Giorni dopo i quali gli audio originali passano al tier Cold.')
param uploadsColdAfterDays int

@description('Giorni dopo i quali le copie normalizzate e i file di testo originali passano al tier Cool.')
param audioCoolAfterDays int

@description('Nome dell\'identità user-assigned delle Functions (stesso resource group).')
param functionIdentityName string

@description('Nome dell\'identità user-assigned del job di trascrizione (stesso resource group).')
param transcriberIdentityName string

@description('Object ID degli utenti sviluppatori che eseguono le Functions in locale (solo dev).')
param developerPrincipalIds array = []

var uploadsContainerName = 'uploads'
var audioContainerName = 'audio'
var transcriptsContainerName = 'transcripts'
var documentsContainerName = 'documents'
var normalizationQueueName = 'normalization'
var transcriptionQueueName = 'transcription'
var transcriptionFallbackQueueName = 'transcription-fallback'
var textExtractionQueueName = 'text-extraction'
var indexingQueueName = 'indexing'
var taggingQueueName = 'tagging'
var uploadExpiryQueueName = 'upload-expiry'

var roles = {
  blobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  blobDataReader: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
  blobDelegator: 'db58b8e5-c6ad-4a2a-8342-4190687cbf4a'
  queueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  queueMessageProcessor: '8a0f0c08-91a1-4084-bc3d-661d67233fed'
  // Necessario allo scaler KEDA del job per leggere la lunghezza della coda.
  queueDataReader: '19e7f393-937e-4f77-808e-94535e297925'
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_GRS'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    // Solo Entra ID: i SAS per l'upload sono user delegation SAS.
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
  }
}

resource lock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: 'lock-${name}'
  scope: storage
  properties: {
    level: 'CanNotDelete'
    notes: 'Contiene i file degli utenti: rimuovere il blocco solo intenzionalmente.'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    isVersioningEnabled: true
    deleteRetentionPolicy: {
      enabled: true
      days: 14
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 14
    }
    cors: {
      corsRules: [
        {
          allowedOrigins: allowedOrigins
          allowedMethods: [
            'PUT'
            'GET'
            'HEAD'
            'OPTIONS'
          ]
          allowedHeaders: [
            '*'
          ]
          exposedHeaders: [
            'ETag'
            'Content-Length'
            'Content-Range'
            'Accept-Ranges'
          ]
          maxAgeInSeconds: 3600
        }
      ]
    }
  }
}

// Originali caricati o registrati dal client: mai cancellati né sovrascritti, salvo eliminazione del
// record o dell'account. Anche i file scartati dalla Function di validazione restano, per poterli
// analizzare. Evento "blob creato" → Function di validazione.
resource uploadsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: uploadsContainerName
  properties: {
    publicAccess: 'None'
  }
}

// Copie normalizzate in Opus scritte dalla Function di normalizzazione.
// Evento "blob creato" → Function che scrive sulla coda di trascrizione.
resource audioContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: audioContainerName
  properties: {
    publicAccess: 'None'
  }
}

// Trascrizioni scritte dal job di ripiego (Whisper) e lette dalla Function, che le salva nel database.
// Evento "blob creato" → Function che scrive sulla coda di indicizzazione.
// Le trascrizioni di Azure AI Speech vanno direttamente nel database e non passano di qui.
// Il job non accede ad Azure SQL; se l'embedding fallisce la trascrizione resta qui.
// Conservazione dopo il salvataggio nel database da definire (nessuna regola di lifecycle per ora).
resource transcriptsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: transcriptsContainerName
  properties: {
    publicAccess: 'None'
  }
}

// File di testo caricati dal client (PDF, Markdown, testo semplice): mai cancellati né sovrascritti, salvo
// eliminazione del record o dell'account; l'utente li scarica dalla pagina del record. Separati dagli
// originali audio per il tier (Cool come l'audio normalizzato, non Cold).
// Evento "blob creato" → Function di validazione dei file di testo.
resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: documentsContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'delete-previous-versions'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
            }
            actions: {
              version: {
                delete: {
                  daysAfterCreationGreaterThan: 30
                }
              }
            }
          }
        }
        {
          // Gli originali si rileggono solo per rinormalizzare: Cold quasi subito.
          name: 'uploads-to-cold'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                '${uploadsContainerName}/'
              ]
            }
            actions: {
              baseBlob: {
                tierToCold: {
                  daysAfterModificationGreaterThan: uploadsColdAfterDays
                }
              }
            }
          }
        }
        {
          name: 'audio-to-cool'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                '${audioContainerName}/'
              ]
            }
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: audioCoolAfterDays
                }
              }
            }
          }
        }
        {
          // Piccoli (al più 1 MB) e scaricabili dall'utente: come l'audio normalizzato, non Cold.
          name: 'documents-to-cool'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                '${documentsContainerName}/'
              ]
            }
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: audioCoolAfterDays
                }
              }
            }
          }
        }
      ]
    }
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource normalizationQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: normalizationQueueName
}

// Letta dalla Function che chiama Azure AI Speech.
resource transcriptionQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: transcriptionQueueName
}

// Scritta dalla Function quando Azure AI Speech non risponde o si superano i limiti; avvia il job di
// trascrizione con Whisper.
resource transcriptionFallbackQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: transcriptionFallbackQueueName
}

// Scritta dalla validazione dei file di testo (e da "riprova"): estrazione del testo nella Function.
resource textExtractionQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: textExtractionQueueName
}

resource indexingQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: indexingQueueName
}

// Scritta dopo l'indicizzazione di un record e alla creazione o modifica di un tag: assegnazione
// automatica dei tag.
resource taggingQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: taggingQueueName
}

// Messaggi ritardati scritti alla richiesta del SAS: eliminano i record il cui upload non arriva.
resource uploadExpiryQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: uploadExpiryQueueName
}

// Sorgente degli eventi "blob creato"; ogni sottoscrizione filtra sul proprio contenitore.
resource eventTopic 'Microsoft.EventGrid/systemTopics@2025-02-15' = {
  name: 'evgt-${name}'
  location: location
  tags: tags
  properties: {
    source: storage.id
    topicType: 'Microsoft.Storage.StorageAccounts'
  }
}

resource functionIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: functionIdentityName
}

resource transcriberIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: transcriberIdentityName
}

// Functions: unico componente che scrive sulle code (e nel database); valida gli originali senza
// eliminarli, legge le trascrizioni, firma i SAS per l'upload ed elimina i blob di un record solo per
// eliminazione del record o dell'account, oppure per un "riprova" o una rigenerazione della
// trascrizione (solo copie normalizzate, trascrizioni ed esiti, mai gli originali).
var functionRoles = [
  roles.blobDataContributor
  roles.blobDelegator
  roles.queueDataContributor
]

resource functionDataRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for roleId in functionRoles: {
    // Nome calcolabile prima del deploy, così what-if può analizzare l'assegnazione.
    name: guid(storage.id, functionIdentityName, roleId)
    scope: storage
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
      principalId: functionIdentity.properties.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

// Sviluppatori: le Functions in locale firmano i SAS con l'utente di Azure CLI, e un user delegation
// SAS non concede più di quanto possa fare chi lo firma. Owner non basta: non dà accesso ai dati.
var developerRoleAssignments = [
  for pair in flatten(map(developerPrincipalIds, principalId => map(functionRoles, roleId => {
    principalId: principalId
    roleId: roleId
  }))): pair
]

resource developerDataRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for assignment in developerRoleAssignments: {
    name: guid(storage.id, assignment.principalId, assignment.roleId)
    scope: storage
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', assignment.roleId)
      principalId: assignment.principalId
      principalType: 'User'
    }
  }
]

// Job: ruoli limitati ai contenitori e alla coda che usa. Legge e consuma solo la propria coda
// (nessuna scrittura sulle code) e scrive solo nel contenitore di destinazione.
var queueRoles = [
  roles.queueDataReader
  roles.queueMessageProcessor
]

// Il job di trascrizione legge solo la coda di ripiego, non quella letta dalla Function.
resource transcriberQueueRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for roleId in queueRoles: {
    name: guid(transcriptionFallbackQueue.id, transcriberIdentityName, roleId)
    scope: transcriptionFallbackQueue
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
      principalId: transcriberIdentity.properties.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

resource transcriberReadRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(audioContainer.id, transcriberIdentityName, roles.blobDataReader)
  scope: audioContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.blobDataReader)
    principalId: transcriberIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource transcriberWriteRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(transcriptsContainer.id, transcriberIdentityName, roles.blobDataContributor)
  scope: transcriptsContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.blobDataContributor)
    principalId: transcriberIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output name string = storage.name
output blobEndpoint string = storage.properties.primaryEndpoints.blob
output queueEndpoint string = storage.properties.primaryEndpoints.queue
output eventTopicName string = eventTopic.name
output uploadsContainerName string = uploadsContainer.name
output audioContainerName string = audioContainer.name
output transcriptsContainerName string = transcriptsContainer.name
output documentsContainerName string = documentsContainer.name
output normalizationQueueName string = normalizationQueue.name
output transcriptionQueueName string = transcriptionQueue.name
output transcriptionFallbackQueueName string = transcriptionFallbackQueue.name
output textExtractionQueueName string = textExtractionQueue.name
output indexingQueueName string = indexingQueue.name
output taggingQueueName string = taggingQueue.name
output uploadExpiryQueueName string = uploadExpiryQueue.name
