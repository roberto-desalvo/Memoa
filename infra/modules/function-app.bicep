// Backend C# .NET (isolated worker) su Azure Functions, piano Flex Consumption.
param location string
param tags object
param baseName string
param hostStorageName string

@description('Nome dell\'identità user-assigned delle Functions (stesso resource group).')
param identityName string

param appInsightsConnectionString string
param allowedOrigins array

param dataBlobEndpoint string
param dataQueueEndpoint string
param uploadsContainerName string
param audioContainerName string
param transcriptsContainerName string
param normalizationQueueName string
param transcriptionQueueName string
param transcriptionFallbackQueueName string
param indexingQueueName string
param uploadExpiryQueueName string
param sqlConnectionString string
param openAiEndpoint string
param embeddingDeploymentName string

@description('Endpoint di Azure AI Speech con il sottodominio personalizzato.')
param speechEndpoint string

@description('Authority del tenant Entra External ID (https://<sottodominio>.ciamlogin.com/<tenant-id>/v2.0).')
param authAuthority string

@description('Client ID della registrazione dell\'API in Entra External ID (claim "aud" dei token).')
param authAudience string

@description('Versione del runtime dotnet-isolated.')
param runtimeVersion string = '10.0'

@description('Numero massimo di istanze (minimo consentito da Flex Consumption: 40).')
param maximumInstanceCount int = 40

param instanceMemoryMB int = 2048

var deploymentContainerName = 'app-package'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: identityName
}

var identityId = identity.id
var identityClientId = identity.properties.clientId

var roles = {
  blobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  queueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  tableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
}

// Storage di sistema delle Functions (host, lease, pacchetto di deploy).
resource hostStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: hostStorageName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
  }
}

resource hostBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: hostStorage
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: hostBlobService
  name: deploymentContainerName
}

resource hostStorageRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for roleId in items(roles): {
    // Nome calcolabile prima del deploy, così what-if può analizzare l'assegnazione.
    name: guid(hostStorage.id, identityName, roleId.value)
    scope: hostStorage
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId.value)
      principalId: identity.properties.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'asp-${baseName}'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'func-${baseName}-${uniqueString(resourceGroup().id)}'
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${hostStorage.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: identityId
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumInstanceCount
        instanceMemoryMB: instanceMemoryMB
      }
      runtime: {
        name: 'dotnet-isolated'
        version: runtimeVersion
      }
    }
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      cors: {
        allowedOrigins: allowedOrigins
        supportCredentials: false
      }
      appSettings: [
        // Storage di sistema tramite identità gestita.
        { name: 'AzureWebJobsStorage__accountName', value: hostStorage.name }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'AzureWebJobsStorage__clientId', value: identityClientId }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'ClientId=${identityClientId};Authorization=AAD' }
        { name: 'AZURE_CLIENT_ID', value: identityClientId }
        // Connessione "DataStorage" usata dai trigger delle code e dai client di blob e code.
        // Gli eventi "blob creato" arrivano dalle sottoscrizioni Event Grid (event-subscriptions.bicep).
        { name: 'DataStorage__blobServiceUri', value: dataBlobEndpoint }
        { name: 'DataStorage__queueServiceUri', value: dataQueueEndpoint }
        { name: 'DataStorage__credential', value: 'managedidentity' }
        { name: 'DataStorage__clientId', value: identityClientId }
        { name: 'Containers__Uploads', value: uploadsContainerName }
        { name: 'Containers__Audio', value: audioContainerName }
        { name: 'Containers__Transcripts', value: transcriptsContainerName }
        { name: 'Queues__Normalization', value: normalizationQueueName }
        { name: 'Queues__Transcription', value: transcriptionQueueName }
        { name: 'Queues__TranscriptionFallback', value: transcriptionFallbackQueueName }
        { name: 'Queues__Indexing', value: indexingQueueName }
        { name: 'Queues__UploadExpiry', value: uploadExpiryQueueName }
        { name: 'Sql__ConnectionString', value: '${sqlConnectionString};User Id=${identityClientId}' }
        { name: 'OpenAI__Endpoint', value: openAiEndpoint }
        { name: 'OpenAI__EmbeddingDeployment', value: embeddingDeploymentName }
        { name: 'Speech__Endpoint', value: speechEndpoint }
        // Validazione dei token di accesso emessi da Entra External ID (middleware delle Functions).
        { name: 'Auth__Authority', value: authAuthority }
        { name: 'Auth__Audience', value: authAudience }
      ]
    }
  }
  dependsOn: [
    hostStorageRoles
    deploymentContainer
  ]
}

output name string = functionApp.name
output defaultHostname string = functionApp.properties.defaultHostName
