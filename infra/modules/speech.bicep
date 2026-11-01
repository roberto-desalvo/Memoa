// Azure AI Speech per la trascrizione (fast transcription), chiamato dalla Function attivata dalla coda di
// trascrizione. Dati elaborati e conservati solo nella regione della risorsa; con la fast transcription
// l'audio è elaborato solo in memoria.
param location string
param tags object
param name string

@description('Identità user-assigned (nomi, nello stesso resource group) autorizzate a chiamare il servizio.')
param userIdentityNames array

@description('Object ID degli utenti sviluppatori che eseguono le Functions in locale (solo dev).')
param developerPrincipalIds array = []

// Cognitive Services Speech User: uso delle API di riconoscimento, senza gestione della risorsa.
var speechUserRoleId = 'f2dc8367-1007-4938-bd23-fe263f013447'

resource userIdentities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = [
  for name in userIdentityNames: {
    name: name
  }
]

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: 'SpeechServices'
  // La fast transcription è disponibile solo con S0 (le ore gratuite di F0 non la includono).
  sku: {
    name: 'S0'
  }
  properties: {
    // Necessario per l'autenticazione Entra ID; non si può più togliere dopo la creazione.
    customSubDomainName: name
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource userRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (name, i) in userIdentityNames: {
    // Nome calcolabile prima del deploy, così what-if può analizzare l'assegnazione.
    name: guid(account.id, name, speechUserRoleId)
    scope: account
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', speechUserRoleId)
      principalId: userIdentities[i].properties.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

// Sviluppatori: le Functions in locale chiamano il servizio con l'utente di Azure CLI, e la risorsa ha
// l'autenticazione locale disattivata. Owner non basta: non dà accesso ai dati.
resource developerRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in developerPrincipalIds: {
    name: guid(account.id, principalId, speechUserRoleId)
    scope: account
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', speechUserRoleId)
      principalId: principalId
      principalType: 'User'
    }
  }
]

// Con il sottodominio personalizzato: https://<name>.cognitiveservices.azure.com/.
output endpoint string = account.properties.endpoint
