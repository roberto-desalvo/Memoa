// Workbook di monitoraggio sul workspace Log Analytics: una scheda per categoria con le query di log-queries/ e una
// scheda con il percorso di un record. Un clic su una riga di "Ultimi errori" o "Record bloccati" imposta il RecordUid.
import { logQueries, recordTimeline } from 'log-queries/queries.bicep'

param location string
param tags object
param environmentName string
param workspaceName string

@description('Limite giornaliero di ingestione in GB, mostrato dalla query sul limite.')
param dailyCapGb string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: workspaceName
}

// Riquadro con una query. width in percentuale ('100' a tutta larghezza); extra si aggiunge al contenuto.
func queryItem(name string, q object, visualization string, width string, extra object) object => {
  type: 3
  name: name
  customWidth: width
  content: union(
    {
      version: 'KqlItem/1.0'
      query: q.query
      size: 0
      title: q.displayName
      timeContextFromParameter: 'TimeRange'
      queryType: 0
      resourceType: 'microsoft.operationalinsights/workspaces'
      visualization: visualization
    },
    extra
  )
}

// Un clic su una riga imposta il parametro RecordUid (scheda "Percorso di un record").
var exportRecordUid = {
  exportFieldName: 'RecordUid'
  exportParameterName: 'RecordUid'
}

// Gruppo di riquadri visibile solo con la scheda selezionata.
func tabGroup(tab string, items array) object => {
  type: 12
  name: 'tab-${tab}'
  content: {
    version: 'NotebookGroup/1.0'
    groupType: 'editable'
    items: items
  }
  conditionalVisibility: {
    parameterName: 'Tab'
    comparison: 'isEqualTo'
    value: tab
  }
}

var tabs = [
  { id: 'health', label: 'Salute' }
  { id: 'pipeline', label: 'Pipeline' }
  { id: 'transcription', label: 'Trascrizione' }
  { id: 'dependencies', label: 'Dipendenze' }
  { id: 'usage', label: 'API e utilizzo' }
  { id: 'costs', label: 'Costi e log' }
  { id: 'privacy', label: 'Privacy' }
  { id: 'record', label: 'Percorso di un record' }
]

var tabLinks = [
  for tab in tabs: {
    id: guid('memoa-workbook', 'tab', tab.id)
    cellValue: 'Tab'
    linkTarget: 'parameter'
    linkLabel: tab.label
    subTarget: tab.id
    style: 'link'
  }
]

var days = 24 * 60 * 60 * 1000

var definition = {
  version: 'Notebook/1.0'
  items: [
    {
      type: 9
      name: 'parameters'
      content: {
        version: 'KqlParameterItem/1.0'
        style: 'pills'
        queryType: 0
        resourceType: 'microsoft.operationalinsights/workspaces'
        parameters: [
          {
            id: guid('memoa-workbook', 'TimeRange')
            version: 'KqlParameterItem/1.0'
            name: 'TimeRange'
            label: 'Intervallo'
            type: 4
            isRequired: true
            value: { durationMs: 7 * days }
            typeSettings: {
              allowCustom: true
              selectableValues: [
                { durationMs: 3600000 }
                { durationMs: 4 * 3600000 }
                { durationMs: days }
                { durationMs: 3 * days }
                { durationMs: 7 * days }
                { durationMs: 14 * days }
                { durationMs: 30 * days }
              ]
            }
          }
          {
            id: guid('memoa-workbook', 'RecordUid')
            version: 'KqlParameterItem/1.0'
            name: 'RecordUid'
            label: 'RecordUid'
            type: 1
            description: 'Record di cui mostrare il percorso: si imposta anche con un clic su una riga di "Ultimi errori" o "Record bloccati".'
          }
        ]
      }
    }
    {
      type: 11
      name: 'tabs'
      content: {
        version: 'LinkItem/1.0'
        style: 'tabs'
        links: tabLinks
      }
    }
    tabGroup('health', [
      queryItem('errors-by-function', logQueries['errors-by-function'], 'timechart', '100', {})
      queryItem('function-success-rate', logQueries['function-success-rate'], 'table', '50', {})
      queryItem('discarded-messages', logQueries['discarded-messages'], 'table', '50', {})
      queryItem('recent-errors', logQueries['recent-errors'], 'table', '100', exportRecordUid)
    ])
    tabGroup('pipeline', [
      queryItem('audio-funnel', logQueries['audio-funnel'], 'barchart', '50', {})
      queryItem('end-to-end-duration', logQueries['end-to-end-duration'], 'table', '50', {})
      queryItem('stuck-records', logQueries['stuck-records'], 'table', '100', union(exportRecordUid, {
        noDataMessage: 'Nessun record bloccato.'
      }))
      queryItem('error-codes', logQueries['error-codes'], 'table', '50', {})
      queryItem('abandoned-uploads', logQueries['abandoned-uploads'], 'barchart', '50', {})
    ])
    tabGroup('transcription', [
      queryItem('speech-vs-fallback', logQueries['speech-vs-fallback'], 'barchart', '50', {})
      queryItem('speech-performance', logQueries['speech-performance'], 'table', '50', {})
      queryItem('job-performance', logQueries['job-performance'], 'table', '50', {})
      queryItem('job-language-detection', logQueries['job-language-detection'], 'table', '50', {})
      queryItem('job-executions', logQueries['job-executions'], 'table', '50', {})
      queryItem('container-apps-warnings', logQueries['container-apps-warnings'], 'table', '50', {})
      queryItem('job-attempts', logQueries['job-attempts'], 'table', '100', exportRecordUid)
    ])
    tabGroup('dependencies', [
      queryItem('dependencies', logQueries.dependencies, 'table', '100', {})
      queryItem('ai-throttling', logQueries['ai-throttling'], 'barchart', '50', {
        noDataMessage: 'Nessuna chiamata fallita.'
      })
      queryItem('cold-starts', logQueries['cold-starts'], 'table', '50', {})
      queryItem('sql-errors', logQueries['sql-errors'], 'table', '100', {
        noDataMessage: 'Nessun errore SQL.'
      })
    ])
    tabGroup('usage', [
      queryItem('api-latency', logQueries['api-latency'], 'table', '50', {})
      queryItem('api-errors', logQueries['api-errors'], 'table', '50', {})
      queryItem('app-activity', logQueries['app-activity'], 'barchart', '50', {})
      queryItem('rejected-tokens', logQueries['rejected-tokens'], 'barchart', '50', {
        noDataMessage: 'Nessun token rifiutato.'
      })
      queryItem('search-results', logQueries['search-results'], 'table', '50', {})
      queryItem('auto-tagging', logQueries['auto-tagging'], 'table', '50', {})
    ])
    tabGroup('costs', [
      queryItem('ingestion-by-table', logQueries['ingestion-by-table'], 'barchart', '100', {})
      queryItem('daily-cap', union(logQueries['daily-cap'], {
        query: replace(logQueries['daily-cap'].query, '{{dailyCapGb}}', dailyCapGb)
      }), 'table', '50', {})
      queryItem('speech-minutes', logQueries['speech-minutes'], 'table', '50', {})
      queryItem('log-volume-by-source', logQueries['log-volume-by-source'], 'table', '100', {})
    ])
    tabGroup('privacy', [
      queryItem('sensitive-data-check', logQueries['sensitive-data-check'], 'table', '100', {
        noDataMessage: 'Nessun dato sensibile trovato.'
      })
    ])
    {
      type: 1
      name: 'record-hint'
      content: {
        json: 'Inserire un RecordUid in alto, oppure fare clic su una riga di "Ultimi errori" (Salute), "Record bloccati" (Pipeline) o "Tentativi falliti del job" (Trascrizione).'
      }
      conditionalVisibilities: [
        { parameterName: 'Tab', comparison: 'isEqualTo', value: 'record' }
        { parameterName: 'RecordUid', comparison: 'isEqualTo', value: '' }
      ]
    }
    {
      type: 12
      name: 'tab-record'
      content: {
        version: 'NotebookGroup/1.0'
        groupType: 'editable'
        items: [
          queryItem('record-timeline', union(recordTimeline, {
            query: 'let recordUid = trim(@"\\s", "{RecordUid}");\n${recordTimeline.query}'
          }), 'table', '100', {
            noDataMessage: 'Nessun log per questo record nell\'intervallo scelto.'
          })
        ]
      }
      // Compare solo con un RecordUid impostato.
      conditionalVisibilities: [
        { parameterName: 'Tab', comparison: 'isEqualTo', value: 'record' }
        { parameterName: 'RecordUid', comparison: 'isNotEqualTo', value: '' }
      ]
    }
  ]
  fallbackResourceIds: [
    workspace.id
  ]
  '$schema': 'https://github.com/Microsoft/Application-Insights-Workbooks/blob/master/schema/workbook.json'
}

resource workbook 'Microsoft.Insights/workbooks@2023-06-01' = {
  // Nome stabile: un deploy successivo aggiorna lo stesso workbook.
  name: guid(resourceGroup().id, 'memoa-monitoring-workbook')
  location: location
  tags: tags
  kind: 'shared'
  properties: {
    displayName: 'Memoa · Monitoraggio (${environmentName})'
    category: 'workbook'
    sourceId: workspace.id
    serializedData: string(definition)
  }
}
