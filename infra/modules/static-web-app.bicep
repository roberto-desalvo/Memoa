// Hosting della PWA Angular (piano Free). Il deploy del contenuto avviene da GitHub Actions
// con il token di deployment, quindi il repository non viene collegato qui.
param location string
param tags object
param baseName string

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'stapp-${baseName}'
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    stagingEnvironmentPolicy: 'Disabled'
    allowConfigFileUpdates: true
  }
}

output name string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname
