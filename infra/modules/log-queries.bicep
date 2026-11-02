// Query salvate nel workspace Log Analytics per il monitoraggio. Elenco e testi in log-queries/ (queries.bicep e un
// file .kql per query, da provare direttamente nel portale). Le query si basano sui message template dei log:
// se cambiano i testi in codice, vanno aggiornate.
import { logQueries, recordTimeline } from 'log-queries/queries.bicep'

param workspaceName string

@description('Limite giornaliero di ingestione in GB, mostrato dalla query sul limite.')
param dailyCapGb string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: workspaceName
}

resource savedQueries 'Microsoft.OperationalInsights/workspaces/savedSearches@2020-08-01' = [
  for q in items(logQueries): {
    parent: workspace
    name: 'memoa-${q.key}'
    properties: {
      category: q.value.category
      displayName: q.value.displayName
      query: replace(q.value.query, '{{dailyCapGb}}', dailyCapGb)
      version: 2
    }
  }
]

// Con parametro: si usa come funzione, es. MemoaRecordTimeline("<RecordUid>").
resource recordTimelineFunction 'Microsoft.OperationalInsights/workspaces/savedSearches@2020-08-01' = {
  parent: workspace
  name: 'memoa-record-timeline'
  properties: {
    category: recordTimeline.category
    displayName: recordTimeline.displayName
    query: recordTimeline.query
    functionAlias: 'MemoaRecordTimeline'
    functionParameters: 'recordUid:string'
    version: 2
  }
}
