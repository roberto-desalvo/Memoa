. (Join-Path $PSScriptRoot 'load-env.ps1')

$account = Get-RequiredEnv 'DATA_STORAGE_ACCOUNT'
$oid = Get-RequiredEnv 'USER_OID'
$recordId = Get-RequiredEnv 'RECORD_ID'
# Stesso blob caricato da upload-normalized-blob.ps1, salvo NORMALIZED_BLOB_NAME
$isError = (Get-OptionalEnv 'NORMALIZED_IS_ERROR' 'false') -eq 'true'
$name = Get-OptionalEnv 'NORMALIZED_BLOB_NAME' $(if ($isError) { "$recordId.error.json" } else { "$recordId.opus" })
$size = 1234
$contentType = if ($isError) { "application/json" } else { "audio/ogg; codecs=opus" }

$event = @(
  @{
    id          = [guid]::NewGuid().ToString()
    eventType   = "Microsoft.Storage.BlobCreated"
    subject     = "/blobServices/default/containers/audio/blobs/$oid/$name"
    eventTime   = (Get-Date).ToUniversalTime().ToString("o")
    dataVersion = "1"
    data        = @{
      api           = "PutBlob"
      url           = "https://$account.blob.core.windows.net/audio/$oid/$name"
      contentType   = $contentType
      contentLength = $size
      blobType      = "BlockBlob"
    }
  }
)

Invoke-RestMethod -Method Post `
  -Uri "http://localhost:7071/runtime/webhooks/EventGrid?functionName=EnqueueTranscription" `
  -Headers @{ "aeg-event-type" = "Notification" } `
  -ContentType "application/json" `
  -Body (ConvertTo-Json -InputObject $event -Depth 5)
