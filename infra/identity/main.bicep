// Memoa – registrazioni delle app (API e frontend) nel tenant Entra External ID di un ambiente.
// Il tenant esterno non ha sottoscrizioni: deploy a livello di tenant, uno per ambiente.
// Deploy: infra/identity/deploy.ps1 -Environment <env> (vedi infra/README.md).
targetScope = 'tenant'

extension 'br:mcr.microsoft.com/bicep/extensions/microsoftgraph/v1.0:1.0.0'

@description('Nome dell\'ambiente.')
@allowed([
  'dev'
  'prod'
])
param environmentName string

@description('Nome del progetto, usato come prefisso dei nomi delle registrazioni.')
param projectName string = 'memoa'

@description('Hostname predefinito della Static Web App dell\'ambiente (output staticWebAppHostname di main.bicep). Vuoto finché l\'infrastruttura non esiste.')
param webHostname string = ''

@description('Redirect URI aggiuntivi del frontend (es. http://localhost:4200 in dev).')
param extraRedirectUris array = []

var baseName = '${projectName}-${environmentName}'

// Scope delegato richiesto dalle Functions nel claim "scp" (Auth__RequiredScope).
var apiScopeName = 'access_as_user'

var redirectUris = concat(empty(webHostname) ? [] : [ 'https://${webHostname}' ], extraRedirectUris)

// App di Microsoft Graph e relativi permessi delegati per il login OpenID Connect.
var graphAppId = '00000003-0000-0000-c000-000000000000'
var graphOpenIdScopeId = '37f7f235-527c-4136-accd-4a02d197296e'
var graphOfflineAccessScopeId = '7427e0e9-2fba-42fe-b0c0-848c9e6a8182'

resource api 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: '${baseName}-api'
  displayName: 'Memoa API (${environmentName})'
  signInAudience: 'AzureADMyOrg'
  // api://<appId> richiederebbe un riferimento a sé stessa: si usa il formato api://<tenant-id>/<nome>.
  identifierUris: [ 'api://${tenant().tenantId}/${baseName}-api' ]
  api: {
    // Token v2: il claim "aud" è il client ID dell'API, come si aspetta Auth__Audience.
    requestedAccessTokenVersion: 2
    oauth2PermissionScopes: [
      {
        id: guid(tenant().tenantId, baseName, apiScopeName)
        value: apiScopeName
        type: 'User'
        isEnabled: true
        adminConsentDisplayName: 'Accesso a Memoa'
        adminConsentDescription: 'Consente al frontend di chiamare l\'API di Memoa per conto dell\'utente.'
        userConsentDisplayName: 'Accesso a Memoa'
        userConsentDescription: 'Consente all\'app di accedere ai tuoi dati in Memoa.'
      }
    ]
  }
}

resource apiServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: api.appId
}

resource web 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: '${baseName}-web'
  displayName: 'Memoa (${environmentName})'
  signInAudience: 'AzureADMyOrg'
  spa: {
    redirectUris: redirectUris
  }
  requiredResourceAccess: [
    {
      resourceAppId: api.appId
      resourceAccess: [
        { id: api.api.oauth2PermissionScopes[0].id, type: 'Scope' }
      ]
    }
    {
      resourceAppId: graphAppId
      resourceAccess: [
        { id: graphOpenIdScopeId, type: 'Scope' }
        { id: graphOfflineAccessScopeId, type: 'Scope' }
      ]
    }
  ]
}

resource webServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: web.appId
}

resource graphServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' existing = {
  appId: graphAppId
}

// Consenso dell'amministratore: nei tenant esterni gli utenti non possono concederlo.
resource webToApiGrant 'Microsoft.Graph/oauth2PermissionGrants@v1.0' = {
  clientId: webServicePrincipal.id
  resourceId: apiServicePrincipal.id
  consentType: 'AllPrincipals'
  scope: apiScopeName
}

resource webToGraphGrant 'Microsoft.Graph/oauth2PermissionGrants@v1.0' = {
  clientId: webServicePrincipal.id
  resourceId: graphServicePrincipal.id
  consentType: 'AllPrincipals'
  scope: 'openid offline_access'
}

@description('Client ID dell\'API: AUTH_AUDIENCE_<ENV> per main.bicep.')
output authAudience string = api.appId

@description('Client ID del frontend, per la configurazione di MSAL.')
output webClientId string = web.appId

@description('Scope da richiedere nel frontend per chiamare l\'API.')
output apiScope string = '${api.identifierUris[0]}/${apiScopeName}'
