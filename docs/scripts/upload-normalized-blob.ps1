. (Join-Path $PSScriptRoot 'load-env.ps1')

$account = Get-RequiredEnv 'DATA_STORAGE_ACCOUNT'
$oid = Get-RequiredEnv 'USER_OID'
$recordId = Get-RequiredEnv 'RECORD_ID'
# false: copia normalizzata (un qualsiasi file .opus); true: esito di errore
$isError = (Get-OptionalEnv 'NORMALIZED_IS_ERROR' 'false') -eq 'true'
$opusFile = Get-OptionalEnv 'NORMALIZED_OPUS_FILE' '.\prova.opus'
$durationSeconds = Get-OptionalEnv 'NORMALIZED_DURATION_SECONDS' '42'
$errorCode = Get-OptionalEnv 'NORMALIZED_ERROR_CODE' 'audio_too_long'

if ($isError) {
  $file = New-TemporaryFile
  "{`"errorCode`":`"$errorCode`"}" | Out-File -Encoding ascii $file

  az storage blob upload --auth-mode login --account-name $account --container-name audio `
    --name "$oid/$recordId.error.json" --file $file --metadata errorcode=$errorCode

  Remove-Item $file
}
else {
  az storage blob upload --auth-mode login --account-name $account --container-name audio `
    --name "$oid/$recordId.opus" --file $opusFile --metadata durationseconds=$durationSeconds
}
