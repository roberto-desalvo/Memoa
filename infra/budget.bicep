// Budget mensile della sottoscrizione: copre entrambi gli ambienti, quindi si distribuisce a parte.
// Deploy: az deployment sub create -l westeurope -f infra/budget.bicep -p contactEmails='["..."]'
// L'importo è nella valuta di fatturazione dell'account.
targetScope = 'subscription'

@description('Importo mensile del budget.')
param amount int = 10

@description('Indirizzi email che ricevono gli avvisi.')
param contactEmails array

@description('Primo giorno del mese di inizio del budget (yyyy-MM-01).')
param startDate string = '${utcNow('yyyy-MM')}-01'

@description('Action group opzionale per reazioni automatiche (resource ID). Vuoto per sole email.')
param actionGroupId string = ''

var contactGroups = empty(actionGroupId) ? [] : [ actionGroupId ]

var actualThresholds = [
  50
  80
  100
]

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: 'budget-memoa-monthly'
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
    }
    notifications: union(
      toObject(
        actualThresholds,
        threshold => 'actual${threshold}',
        threshold => {
          enabled: true
          operator: 'GreaterThanOrEqualTo'
          threshold: threshold
          thresholdType: 'Actual'
          contactEmails: contactEmails
          contactGroups: contactGroups
        }
      ),
      {
        forecasted100: {
          enabled: true
          operator: 'GreaterThanOrEqualTo'
          threshold: 100
          thresholdType: 'Forecasted'
          contactEmails: contactEmails
          contactGroups: contactGroups
        }
      }
    )
  }
}
