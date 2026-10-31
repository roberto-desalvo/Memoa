# Distribuisce le registrazioni delle app (infra/identity/main.bicep) nel tenant Entra External ID
# di un ambiente e salva gli ID ottenuti in infra/.env e nelle variabili d'ambiente della sessione.
# Uso, con az collegato alla sottoscrizione Azure: .\infra\identity\deploy.ps1 -Environment dev
#
# Il tenant esterno si ricava da AUTH_AUTHORITY_<ENV>. Alla fine az torna sulla sottoscrizione di partenza.

param(
    [Parameter(Mandatory)]
    [ValidateSet('dev', 'prod')]
    [string] $Environment,

    [string] $Location = 'westeurope'
)

$envFile = Join-Path $PSScriptRoot '..\.env'
$suffix = $Environment.ToUpper()

# Aggiorna (o aggiunge) NOME=valore in infra/.env e imposta la variabile nella sessione.
function Save-EnvValue([string] $name, [string] $value) {
    $lines = @()
    if (Test-Path $envFile) { $lines = @([IO.File]::ReadAllLines($envFile)) }

    $found = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^\s*$name\s*=") { $lines[$i] = "$name=$value"; $found = $true }
    }
    if (-not $found) { $lines += "$name=$value" }

    [IO.File]::WriteAllLines($envFile, [string[]] $lines, [Text.UTF8Encoding]::new($false))
    Set-Item "Env:$name" $value
    Write-Host "Salvata $name"
}

. (Join-Path $PSScriptRoot '..\load-env.ps1')

$authority = [Environment]::GetEnvironmentVariable("AUTH_AUTHORITY_$suffix")
if ($authority -notmatch '/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/') {
    throw "AUTH_AUTHORITY_$suffix mancante o senza tenant ID (atteso https://<sottodominio>.ciamlogin.com/<tenant-id>/v2.0)."
}
$tenantId = $Matches[1]

$previousSubscription = az account show --query id -o tsv
if ($LASTEXITCODE -ne 0) { throw 'az non risulta collegato: eseguire az login sulla sottoscrizione Azure.' }

# Hostname della Static Web App dal deploy dell'infrastruttura, se esiste già.
$webHostname = az deployment sub show -n "memoa-$Environment" --query properties.outputs.staticWebAppHostname.value -o tsv 2>$null
if ($LASTEXITCODE -ne 0 -or -not $webHostname) {
    Write-Warning "Deploy memoa-$Environment non trovato: redirect URI senza la Static Web App. Ripetere dopo il deploy dell'infrastruttura."
    $webHostname = ''
}

try {
    # Con --allow-no-subscriptions l'account del tenant esterno ha come ID il tenant stesso.
    az account set -s $tenantId 2>$null
    if ($LASTEXITCODE -ne 0) {
        az login --tenant $tenantId --allow-no-subscriptions | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Login sul tenant $tenantId non riuscito." }
        az account set -s $tenantId
    }

    $deployArgs = @(
        'deployment', 'tenant', 'create',
        '-l', $Location,
        '-n', "memoa-identity-$Environment",
        '-p', (Join-Path $PSScriptRoot "main.$Environment.bicepparam"),
        '--query', 'properties.outputs', '-o', 'json'
    )
    if ($webHostname) { $deployArgs += @('-p', "webHostname=$webHostname") }

    $json = az @deployArgs
    if ($LASTEXITCODE -ne 0) { throw 'Deploy delle registrazioni non riuscito.' }
    $outputs = ($json -join "`n") | ConvertFrom-Json
}
finally {
    az account set -s $previousSubscription
}

Save-EnvValue "AUTH_AUDIENCE_$suffix" $outputs.authAudience.value
Save-EnvValue "AUTH_WEB_CLIENT_ID_$suffix" $outputs.webClientId.value
Save-EnvValue "AUTH_API_SCOPE_$suffix" $outputs.apiScope.value
