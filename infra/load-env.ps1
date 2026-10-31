# Carica infra/.env come variabili d'ambiente della sessione PowerShell corrente,
# lette dai file .bicepparam tramite readEnvironmentVariable().
# Uso (con il punto iniziale, per restare nella sessione): . .\infra\load-env.ps1

$envFile = Join-Path $PSScriptRoot '.env'
if (-not (Test-Path $envFile)) {
    throw "File $envFile non trovato: copiare infra/.env.example in infra/.env e compilarlo."
}

foreach ($line in Get-Content $envFile) {
    $line = $line.Trim()
    if ($line -eq '' -or $line.StartsWith('#')) { continue }

    $name, $value = $line -split '=', 2
    $name = $name.Trim()
    $value = $value.Trim().Trim('"').Trim("'")

    # Le variabili vuote vengono rimosse, così si applica il valore di ripiego del .bicepparam.
    if ($value -eq '') {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
    else {
        Set-Item "Env:$name" $value
        Write-Host "Impostata $name"
    }
}
