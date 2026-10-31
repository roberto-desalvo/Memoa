. (Join-Path $PSScriptRoot 'load-env.ps1')

$account = Get-RequiredEnv 'DATA_STORAGE_ACCOUNT'
$oid = Get-RequiredEnv 'USER_OID'
$recordId = Get-RequiredEnv 'RECORD_ID'
# false: trascrizione (testo fittizio); true: esito di errore
$isError = (Get-OptionalEnv 'TRANSCRIPT_IS_ERROR' 'false') -eq 'true'
$language = Get-OptionalEnv 'TRANSCRIPT_LANGUAGE' 'it'
$errorCode = Get-OptionalEnv 'TRANSCRIPT_ERROR_CODE' 'no_speech'

$file = New-TemporaryFile

if ($isError) {
  "{`"errorCode`":`"$errorCode`"}" | Out-File -Encoding ascii $file

  az storage blob upload --auth-mode login --account-name $account --container-name transcripts `
    --name "$oid/$recordId.error.json" --file $file --metadata errorcode=$errorCode
}
else {
  "Trascrizione di prova." | Out-File -Encoding utf8 $file

  az storage blob upload --auth-mode login --account-name $account --container-name transcripts `
    --name "$oid/$recordId.txt" --file $file --content-type "text/plain; charset=utf-8" `
    --metadata language=$language engine=manual
}

Remove-Item $file
