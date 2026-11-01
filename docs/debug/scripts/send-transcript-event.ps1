. (Join-Path $PSScriptRoot 'load-env.ps1')

$account = Get-RequiredEnv 'DATA_STORAGE_ACCOUNT'
$oid = Get-RequiredEnv 'USER_OID'
$recordId = Get-RequiredEnv 'RECORD_ID'
# Stesso blob caricato da upload-transcript-blob.ps1, salvo TRANSCRIPT_BLOB_NAME
$isError = (Get-OptionalEnv 'TRANSCRIPT_IS_ERROR' 'false') -eq 'true'
$name = Get-OptionalEnv 'TRANSCRIPT_BLOB_NAME' $(if ($isError) { "$recordId.error.json" } else { "$recordId.txt" })
$size = 1234
$contentType = if ($isError) { "application/json" } else { "text/plain; charset=utf-8" }

$event = @(
  @{
    id          = [guid]::NewGuid().ToString()
    eventType   = "Microsoft.Storage.BlobCreated"
    subject     = "/blobServices/default/containers/transcripts/blobs/$oid/$name"
    eventTime   = (Get-Date).ToUniversalTime().ToString("o")
    dataVersion = "1"
    data        = @{
      api           = "PutBlob"
      url           = "https://$account.blob.core.windows.net/transcripts/$oid/$name"
      contentType   = $contentType
      contentLength = $size
      blobType      = "BlockBlob"
    }
  }
)

Invoke-RestMethod -Method Post `
  -Uri "http://localhost:7071/runtime/webhooks/EventGrid?functionName=EnqueueIndexing" `
  -Headers @{ "aeg-event-type" = "Notification" } `
  -ContentType "application/json" `
  -Body (ConvertTo-Json -InputObject $event -Depth 5)
