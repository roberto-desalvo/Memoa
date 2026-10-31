// Log Analytics + Application Insights basato su workspace, entro le soglie gratuite.
param location string
param tags object
param baseName string

@description('Limite giornaliero di ingestione in GB (stringa, es. "0.1").')
param dailyCapGb string

@description('Identità user-assigned (nomi, nello stesso resource group) autorizzate a inviare telemetria con Entra ID.')
param publisherIdentityNames array

var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'

resource publisherIdentities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = [
  for name in publisherIdentityNames: {
    name: name
  }
]

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${baseName}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json(dailyCapGb)
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${baseName}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    // Solo autenticazione Entra ID per l'invio della telemetria.
    DisableLocalAuth: true
    // Niente indirizzi IP dei client nei log (GDPR).
    DisableIpMasking: false
  }
}

resource publisherRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (name, i) in publisherIdentityNames: {
    // Nome calcolabile prima del deploy, così what-if può analizzare l'assegnazione.
    name: guid(appInsights.id, name, monitoringMetricsPublisherRoleId)
    scope: appInsights
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRoleId)
      principalId: publisherIdentities[i].properties.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output logAnalyticsWorkspaceName string = workspace.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
