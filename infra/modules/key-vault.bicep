// Key Vault per le sole credenziali non sostituibili da identità gestite (es. token GHCR).
param location string
param tags object
param name string
param enablePurgeProtection bool

@secure()
param ghcrToken string

@description('Identità user-assigned (nomi, nello stesso resource group) autorizzate a leggere i segreti.')
param secretReaderIdentityNames array

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource secretReaderIdentities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = [
  for name in secretReaderIdentityNames: {
    name: name
  }
]

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    // La purge protection non è disattivabile una volta attivata: solo in prod.
    enablePurgeProtection: enablePurgeProtection ? true : null
    publicNetworkAccess: 'Enabled'
  }
}

resource ghcrTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(ghcrToken)) {
  parent: keyVault
  name: 'ghcr-token'
  properties: {
    value: ghcrToken
    contentType: 'GitHub PAT (read:packages)'
  }
}

resource readerRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (name, i) in secretReaderIdentityNames: {
    // Nome calcolabile prima del deploy, così what-if può analizzare l'assegnazione.
    name: guid(keyVault.id, name, keyVaultSecretsUserRoleId)
    scope: keyVault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
      principalId: secretReaderIdentities[i].properties.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

// Espone solo l'URI del segreto: il token è usato unicamente per verificarne la presenza.
#disable-next-line outputs-should-not-contain-secrets
output ghcrTokenSecretUri string = empty(ghcrToken) ? '' : ghcrTokenSecret!.properties.secretUri
