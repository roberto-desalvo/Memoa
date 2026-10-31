// Identità gestite user-assigned: create prima delle risorse, così i ruoli RBAC
// sono assegnabili senza dipendenze circolari.
param location string
param tags object
param functionIdentityName string
param normalizerIdentityName string
param transcriberIdentityName string

resource functionIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: functionIdentityName
  location: location
  tags: tags
}

resource normalizerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: normalizerIdentityName
  location: location
  tags: tags
}

resource transcriberIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: transcriberIdentityName
  location: location
  tags: tags
}

output functionIdentityId string = functionIdentity.id
output functionIdentityName string = functionIdentity.name
output functionClientId string = functionIdentity.properties.clientId
output functionPrincipalId string = functionIdentity.properties.principalId
output normalizerIdentityId string = normalizerIdentity.id
output normalizerIdentityName string = normalizerIdentity.name
output normalizerClientId string = normalizerIdentity.properties.clientId
output normalizerPrincipalId string = normalizerIdentity.properties.principalId
output transcriberIdentityId string = transcriberIdentity.id
output transcriberIdentityName string = transcriberIdentity.name
output transcriberClientId string = transcriberIdentity.properties.clientId
output transcriberPrincipalId string = transcriberIdentity.properties.principalId
