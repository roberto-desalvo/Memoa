# Carica docs/debug/scripts/.env come variabili d'ambiente, per gli script di simulazione degli eventi.
# Richiamato in testa a ogni script (con il punto iniziale): . (Join-Path $PSScriptRoot 'load-env.ps1')

$envFile = Join-Path $PSScriptRoot '.env'
if (-not (Test-Path $envFile)) {
    throw "File $envFile non trovato: copiare docs/debug/scripts/.env.example in docs/debug/scripts/.env e compilarlo."
}

foreach ($line in Get-Content $envFile) {
    $line = $line.Trim()
    if ($line -eq '' -or $line.StartsWith('#')) { continue }

    $name, $value = $line -split '=', 2
    $name = $name.Trim()
    $value = $value.Trim().Trim('"').Trim("'")

    # Le variabili vuote vengono rimosse, così si applica il valore predefinito dello script.
    if ($value -eq '') {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
    else {
        Set-Item "Env:$name" $value
    }
}

# Valore di una variabile obbligatoria.
function Get-RequiredEnv([string] $name) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Variabile $name mancante in docs/debug/scripts/.env."
    }
    return $value
}

# Valore di una variabile facoltativa, o il valore predefinito se manca.
function Get-OptionalEnv([string] $name, [string] $default) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($value)) { return $default }
    return $value
}
