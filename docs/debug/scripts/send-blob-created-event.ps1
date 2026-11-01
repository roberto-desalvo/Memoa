. (Join-Path $PSScriptRoot 'load-env.ps1')

$account = Get-RequiredEnv 'DATA_STORAGE_ACCOUNT'
$oid = Get-RequiredEnv 'USER_OID'
$recordId = Get-RequiredEnv 'RECORD_ID'
$size = [long](Get-OptionalEnv 'UPLOAD_SIZE' '391502')
$contentType = Get-OptionalEnv 'UPLOAD_CONTENT_TYPE' 'audio/webm;codecs=opus'

$event = @(
  @{
    id          = [guid]::NewGuid().ToString()
    eventType   = "Microsoft.Storage.BlobCreated"
    subject     = "/blobServices/default/containers/uploads/blobs/$oid/$recordId"
    eventTime   = (Get-Date).ToUniversalTime().ToString("o")
    dataVersion = "1"
    data        = @{
      api           = "PutBlob"
      url           = "https://$account.blob.core.windows.net/uploads/$oid/$recordId"
      contentType   = $contentType
      contentLength = $size
      blobType      = "BlockBlob"
    }
  }
)

Invoke-RestMethod -Method Post `
  -Uri "http://localhost:7071/runtime/webhooks/EventGrid?functionName=ValidateUpload" `
  -Headers @{ "aeg-event-type" = "Notification" } `
  -ContentType "application/json" `
  -Body (ConvertTo-Json -InputObject $event -Depth 5)
