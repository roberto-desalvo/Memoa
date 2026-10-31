using 'main.bicep'

param environmentName = 'prod'

// Hostname della Static Web App: lo passa deploy.ps1 leggendolo dal deploy di infra/main.bicep.
param webHostname = ''
