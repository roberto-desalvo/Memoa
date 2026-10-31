// Database Azure SQL con l'offerta gratuita (serverless, pausa automatica a quota esaurita)
// su un server esistente condiviso, non gestito da questo template: amministratore Entra ID
// e regola firewall per i servizi Azure sono già configurati sul server.
// Gli utenti del database per le identità gestite si creano con T-SQL dopo il deploy (vedi infra/README.md).
// Va distribuito nel resource group del server.
param location string
param tags object
param serverName string
param databaseName string

resource server 'Microsoft.Sql/servers@2023-08-01-preview' existing = {
  name: serverName
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: server
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    useFreeLimit: true
    // A quota mensile esaurita il database va in pausa invece di generare costi.
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

output serverFqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
// Il client ID dell'identità va aggiunto da chi consuma (User Id=<clientId>).
output managedIdentityConnectionStringPrefix string = 'Server=tcp:${server.properties.fullyQualifiedDomainName},1433;Database=${database.name};Encrypt=True;Authentication=Active Directory Managed Identity'
