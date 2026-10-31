// Identità usata dalle pipeline GitHub Actions dell'ambiente (OIDC, nessun segreto):
// federated credential sull'environment GitHub omonimo e ruoli limitati a Functions, Static Web App e
// Container Apps Jobs (aggiornamento dell'immagine).
// L'utente del database per le migration si crea con T-SQL dopo il deploy (vedi infra/README.md).
param location string
param tags object
param identityName string

@description('Repository GitHub nel formato <owner>/<repo>.')
param githubRepository string

@description('Nome dell\'environment GitHub da cui girano i job di deploy.')
param githubEnvironment string

param functionAppName string
param staticWebAppName string

@description('Container Apps Jobs di cui la pipeline aggiorna l\'immagine.')
param containerAppJobNames array

var roles = {
  // Deploy del pacchetto (OneDeploy) e lettura dell'hostname.
  websiteContributor: 'de139f84-1756-47ae-9be6-808fbbe84772'
  // Lettura del token di deployment: nessun ruolo predefinito più ristretto lo consente.
  contributor: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
  // Aggiornamento dell'immagine del job, senza accesso alle altre risorse del resource group.
  containerAppsJobsContributor: '4e3d2b60-56ae-4dc6-a233-09c8e5a82e68'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

resource githubCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-${githubEnvironment}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepository}:environment:${githubEnvironment}'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: functionAppName
}

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' existing = {
  name: staticWebAppName
}

resource functionAppRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionApp.id, identityName, roles.websiteContributor)
  scope: functionApp
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.websiteContributor)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource staticWebAppRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(staticWebApp.id, identityName, roles.contributor)
  scope: staticWebApp
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.contributor)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource jobs 'Microsoft.App/jobs@2025-01-01' existing = [for jobName in containerAppJobNames: {
  name: jobName
}]

resource jobRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (jobName, i) in containerAppJobNames: {
  name: guid(jobs[i].id, identityName, roles.containerAppsJobsContributor)
  scope: jobs[i]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.containerAppsJobsContributor)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}]

output name string = identity.name
output clientId string = identity.properties.clientId
