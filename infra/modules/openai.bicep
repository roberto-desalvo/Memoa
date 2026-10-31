// Azure OpenAI per gli embedding, con deployment Data Zone Standard (EU)
// così l'elaborazione resta nell'Unione Europea.
param location string
param tags object
param name string

@description('Identità user-assigned (nomi, nello stesso resource group) autorizzate a chiamare il modello.')
param userIdentityNames array

@description('Capacità del deployment, in migliaia di token al minuto.')
param embeddingCapacity int = 50

var cognitiveServicesOpenAiUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
var embeddingDeploymentName = 'text-embedding-3-small'

resource userIdentities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = [
  for name in userIdentityNames: {
    name: name
  }
]

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: name
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: account
  name: embeddingDeploymentName
  sku: {
    name: 'DataZoneStandard'
    capacity: embeddingCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-small'
      version: '1'
    }
    versionUpgradeOption: 'NoAutoUpgrade'
  }
}

resource userRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (name, i) in userIdentityNames: {
    // Nome calcolabile prima del deploy, così what-if può analizzare l'assegnazione.
    name: guid(account.id, name, cognitiveServicesOpenAiUserRoleId)
    scope: account
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAiUserRoleId)
      principalId: userIdentities[i].properties.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output endpoint string = account.properties.endpoint
output embeddingDeploymentName string = embeddingDeployment.name
