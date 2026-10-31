using 'main.bicep'

param environmentName = 'dev'

// Hostname della Static Web App: lo passa deploy.ps1 leggendolo dal deploy di infra/main.bicep.
param webHostname = ''

param extraRedirectUris = [
  'http://localhost:4200'
]
