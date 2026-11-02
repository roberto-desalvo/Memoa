// Elenco delle query di monitoraggio, condiviso da log-queries.bicep (query salvate) e monitoring-workbook.bicep.
// Il testo di ogni query è nel file .kql omonimo; {{dailyCapGb}} va sostituito da chi la usa.

@export()
var categories = {
  health: 'Memoa · Salute'
  pipeline: 'Memoa · Pipeline'
  transcription: 'Memoa · Trascrizione'
  dependencies: 'Memoa · Dipendenze'
  usage: 'Memoa · API e utilizzo'
  costs: 'Memoa · Costi e log'
  privacy: 'Memoa · Privacy'
}

@export()
var logQueries = {
  'errors-by-function': {
    displayName: '01 Errori per Function'
    category: categories.health
    query: loadTextContent('01-errors-by-function.kql')
  }
  'function-success-rate': {
    displayName: '02 Esecuzioni fallite per Function'
    category: categories.health
    query: loadTextContent('02-function-success-rate.kql')
  }
  'recent-errors': {
    displayName: '03 Ultimi errori'
    category: categories.health
    query: loadTextContent('03-recent-errors.kql')
  }
  'discarded-messages': {
    displayName: '04 Messaggi di coda scartati'
    category: categories.health
    query: loadTextContent('04-discarded-messages.kql')
  }
  'audio-funnel': {
    displayName: '06 Funnel delle note audio'
    category: categories.pipeline
    query: loadTextContent('06-audio-funnel.kql')
  }
  'stuck-records': {
    displayName: '07 Record bloccati'
    category: categories.pipeline
    query: loadTextContent('07-stuck-records.kql')
  }
  'end-to-end-duration': {
    displayName: '08 Durata end-to-end'
    category: categories.pipeline
    query: loadTextContent('08-end-to-end-duration.kql')
  }
  'error-codes': {
    displayName: '09 Codici d\'errore'
    category: categories.pipeline
    query: loadTextContent('09-error-codes.kql')
  }
  'abandoned-uploads': {
    displayName: '10 Upload abbandonati'
    category: categories.pipeline
    query: loadTextContent('10-abandoned-uploads.kql')
  }
  'speech-vs-fallback': {
    displayName: '11 Azure AI Speech o ripiego'
    category: categories.transcription
    query: loadTextContent('11-speech-vs-fallback.kql')
  }
  'speech-performance': {
    displayName: '12 Prestazioni di Azure AI Speech'
    category: categories.transcription
    query: loadTextContent('12-speech-performance.kql')
  }
  'job-performance': {
    displayName: '13 Prestazioni del job per motore'
    category: categories.transcription
    query: loadTextContent('13-job-performance.kql')
  }
  'job-language-detection': {
    displayName: '14 Rilevamento della lingua nel job'
    category: categories.transcription
    query: loadTextContent('14-job-language-detection.kql')
  }
  'job-executions': {
    displayName: '15 Esecuzioni del job'
    category: categories.transcription
    query: loadTextContent('15-job-executions.kql')
  }
  'container-apps-warnings': {
    displayName: '15b Avvisi di Container Apps'
    category: categories.transcription
    query: loadTextContent('15b-container-apps-warnings.kql')
  }
  'job-attempts': {
    displayName: '16 Tentativi falliti del job'
    category: categories.transcription
    query: loadTextContent('16-job-attempts.kql')
  }
  dependencies: {
    displayName: '17 Latenza ed errori delle dipendenze'
    category: categories.dependencies
    query: loadTextContent('17-dependencies.kql')
  }
  'ai-throttling': {
    displayName: '18 Errori di Azure OpenAI e Speech'
    category: categories.dependencies
    query: loadTextContent('18-ai-throttling.kql')
  }
  'sql-errors': {
    displayName: '19 Errori di Azure SQL'
    category: categories.dependencies
    query: loadTextContent('19-sql-errors.kql')
  }
  'cold-starts': {
    displayName: '20 Cold start delle Functions'
    category: categories.dependencies
    query: loadTextContent('20-cold-starts.kql')
  }
  'api-latency': {
    displayName: '21 Latenza delle API'
    category: categories.usage
    query: loadTextContent('21-api-latency.kql')
  }
  'api-errors': {
    displayName: '22 Errori HTTP'
    category: categories.usage
    query: loadTextContent('22-api-errors.kql')
  }
  'rejected-tokens': {
    displayName: '23 Token rifiutati'
    category: categories.usage
    query: loadTextContent('23-rejected-tokens.kql')
  }
  'app-activity': {
    displayName: '24 Attività dell\'app'
    category: categories.usage
    query: loadTextContent('24-app-activity.kql')
  }
  'search-results': {
    displayName: '25 Ricerche senza risultati'
    category: categories.usage
    query: loadTextContent('25-search-results.kql')
  }
  'auto-tagging': {
    displayName: '26 Tag assegnati in automatico'
    category: categories.usage
    query: loadTextContent('26-auto-tagging.kql')
  }
  'ingestion-by-table': {
    displayName: '27 Ingestione per tabella'
    category: categories.costs
    query: loadTextContent('27-ingestion-by-table.kql')
  }
  'log-volume-by-source': {
    displayName: '28 Volume dei log per origine'
    category: categories.costs
    query: loadTextContent('28-log-volume-by-source.kql')
  }
  'daily-cap': {
    displayName: '29 Limite giornaliero'
    category: categories.costs
    query: loadTextContent('29-daily-cap.kql')
  }
  'speech-minutes': {
    displayName: '30 Minuti trascritti da Azure AI Speech'
    category: categories.costs
    query: loadTextContent('30-speech-minutes.kql')
  }
  'sensitive-data-check': {
    displayName: '31 Controllo dei dati sensibili'
    category: categories.privacy
    query: loadTextContent('31-sensitive-data-check.kql')
  }
}

// Usa il parametro recordUid: come query salvata è una funzione, nel workbook lo dichiara chi la usa.
@export()
var recordTimeline = {
  displayName: '05 Percorso di un record'
  category: categories.pipeline
  query: loadTextContent('05-record-timeline.kql')
}
