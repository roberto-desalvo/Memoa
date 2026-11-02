// Query salvate nel workspace Log Analytics per il monitoraggio. Il testo di ogni query è nel file .kql omonimo in
// log-queries/, così si può provare direttamente nel portale. Le query si basano sui message template dei log:
// se cambiano i testi in codice, vanno aggiornate.
param workspaceName string

@description('Limite giornaliero di ingestione in GB, mostrato dalla query sul limite.')
param dailyCapGb string

var categories = {
  health: 'Memoa · Salute'
  pipeline: 'Memoa · Pipeline'
  transcription: 'Memoa · Trascrizione'
  dependencies: 'Memoa · Dipendenze'
  usage: 'Memoa · API e utilizzo'
  costs: 'Memoa · Costi e log'
  privacy: 'Memoa · Privacy'
}

var queries = [
  {
    name: 'errors-by-function'
    displayName: '01 Errori per Function'
    category: categories.health
    query: loadTextContent('log-queries/01-errors-by-function.kql')
  }
  {
    name: 'function-success-rate'
    displayName: '02 Esecuzioni fallite per Function'
    category: categories.health
    query: loadTextContent('log-queries/02-function-success-rate.kql')
  }
  {
    name: 'recent-errors'
    displayName: '03 Ultimi errori'
    category: categories.health
    query: loadTextContent('log-queries/03-recent-errors.kql')
  }
  {
    name: 'discarded-messages'
    displayName: '04 Messaggi di coda scartati'
    category: categories.health
    query: loadTextContent('log-queries/04-discarded-messages.kql')
  }
  {
    name: 'audio-funnel'
    displayName: '06 Funnel delle note audio'
    category: categories.pipeline
    query: loadTextContent('log-queries/06-audio-funnel.kql')
  }
  {
    name: 'stuck-records'
    displayName: '07 Record bloccati'
    category: categories.pipeline
    query: loadTextContent('log-queries/07-stuck-records.kql')
  }
  {
    name: 'end-to-end-duration'
    displayName: '08 Durata end-to-end'
    category: categories.pipeline
    query: loadTextContent('log-queries/08-end-to-end-duration.kql')
  }
  {
    name: 'error-codes'
    displayName: '09 Codici d\'errore'
    category: categories.pipeline
    query: loadTextContent('log-queries/09-error-codes.kql')
  }
  {
    name: 'abandoned-uploads'
    displayName: '10 Upload abbandonati'
    category: categories.pipeline
    query: loadTextContent('log-queries/10-abandoned-uploads.kql')
  }
  {
    name: 'speech-vs-fallback'
    displayName: '11 Azure AI Speech o ripiego'
    category: categories.transcription
    query: loadTextContent('log-queries/11-speech-vs-fallback.kql')
  }
  {
    name: 'speech-performance'
    displayName: '12 Prestazioni di Azure AI Speech'
    category: categories.transcription
    query: loadTextContent('log-queries/12-speech-performance.kql')
  }
  {
    name: 'job-performance'
    displayName: '13 Prestazioni del job per motore'
    category: categories.transcription
    query: loadTextContent('log-queries/13-job-performance.kql')
  }
  {
    name: 'job-language-detection'
    displayName: '14 Rilevamento della lingua nel job'
    category: categories.transcription
    query: loadTextContent('log-queries/14-job-language-detection.kql')
  }
  {
    name: 'job-executions'
    displayName: '15 Esecuzioni del job'
    category: categories.transcription
    query: loadTextContent('log-queries/15-job-executions.kql')
  }
  {
    name: 'container-apps-warnings'
    displayName: '15b Avvisi di Container Apps'
    category: categories.transcription
    query: loadTextContent('log-queries/15b-container-apps-warnings.kql')
  }
  {
    name: 'job-attempts'
    displayName: '16 Tentativi falliti del job'
    category: categories.transcription
    query: loadTextContent('log-queries/16-job-attempts.kql')
  }
  {
    name: 'dependencies'
    displayName: '17 Latenza ed errori delle dipendenze'
    category: categories.dependencies
    query: loadTextContent('log-queries/17-dependencies.kql')
  }
  {
    name: 'ai-throttling'
    displayName: '18 Errori di Azure OpenAI e Speech'
    category: categories.dependencies
    query: loadTextContent('log-queries/18-ai-throttling.kql')
  }
  {
    name: 'sql-errors'
    displayName: '19 Errori di Azure SQL'
    category: categories.dependencies
    query: loadTextContent('log-queries/19-sql-errors.kql')
  }
  {
    name: 'cold-starts'
    displayName: '20 Cold start delle Functions'
    category: categories.dependencies
    query: loadTextContent('log-queries/20-cold-starts.kql')
  }
  {
    name: 'api-latency'
    displayName: '21 Latenza delle API'
    category: categories.usage
    query: loadTextContent('log-queries/21-api-latency.kql')
  }
  {
    name: 'api-errors'
    displayName: '22 Errori HTTP'
    category: categories.usage
    query: loadTextContent('log-queries/22-api-errors.kql')
  }
  {
    name: 'rejected-tokens'
    displayName: '23 Token rifiutati'
    category: categories.usage
    query: loadTextContent('log-queries/23-rejected-tokens.kql')
  }
  {
    name: 'app-activity'
    displayName: '24 Attività dell\'app'
    category: categories.usage
    query: loadTextContent('log-queries/24-app-activity.kql')
  }
  {
    name: 'search-results'
    displayName: '25 Ricerche senza risultati'
    category: categories.usage
    query: loadTextContent('log-queries/25-search-results.kql')
  }
  {
    name: 'auto-tagging'
    displayName: '26 Tag assegnati in automatico'
    category: categories.usage
    query: loadTextContent('log-queries/26-auto-tagging.kql')
  }
  {
    name: 'ingestion-by-table'
    displayName: '27 Ingestione per tabella'
    category: categories.costs
    query: loadTextContent('log-queries/27-ingestion-by-table.kql')
  }
  {
    name: 'log-volume-by-source'
    displayName: '28 Volume dei log per origine'
    category: categories.costs
    query: loadTextContent('log-queries/28-log-volume-by-source.kql')
  }
  {
    name: 'daily-cap'
    displayName: '29 Limite giornaliero'
    category: categories.costs
    query: replace(loadTextContent('log-queries/29-daily-cap.kql'), '{{dailyCapGb}}', dailyCapGb)
  }
  {
    name: 'speech-minutes'
    displayName: '30 Minuti trascritti da Azure AI Speech'
    category: categories.costs
    query: loadTextContent('log-queries/30-speech-minutes.kql')
  }
  {
    name: 'sensitive-data-check'
    displayName: '31 Controllo dei dati sensibili'
    category: categories.privacy
    query: loadTextContent('log-queries/31-sensitive-data-check.kql')
  }
]

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: workspaceName
}

resource savedQueries 'Microsoft.OperationalInsights/workspaces/savedSearches@2020-08-01' = [
  for q in queries: {
    parent: workspace
    name: 'memoa-${q.name}'
    properties: {
      category: q.category
      displayName: q.displayName
      query: q.query
      version: 2
    }
  }
]

// Con parametro: si usa come funzione, es. MemoaRecordTimeline("<RecordUid>").
resource recordTimeline 'Microsoft.OperationalInsights/workspaces/savedSearches@2020-08-01' = {
  parent: workspace
  name: 'memoa-record-timeline'
  properties: {
    category: categories.pipeline
    displayName: '05 Percorso di un record'
    query: loadTextContent('log-queries/05-record-timeline.kql')
    functionAlias: 'MemoaRecordTimeline'
    functionParameters: 'recordUid:string'
    version: 2
  }
}
