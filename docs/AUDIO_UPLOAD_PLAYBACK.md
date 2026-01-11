# Audio Upload & Playback Implementation

## Overview

L'implementazione audio in Memoa segue un'architettura **client-server con storage privato** su Supabase:

- **Upload**: FE carica il blob nel bucket privato `audio/` con path `audio/{userId}/{entryId}.webm`
- **Playback**: FE genera signed URL temporanee (24h) per accedere ai file privati
- **Database**: Solo i metadati (path, mime, durata, size) sono salvati in PostgreSQL

---

## Architecture

### 1. Upload Flow

```
┌─────────────────────────────────────────────────────────────┐
│ Frontend (EntryForm + useEntryForm)                          │
│                                                              │
│  1. useAudioRecorder.ts → blob + metadata                   │
│  2. useEntryForm.submitEntry()                               │
│     └─> supabaseStorage.uploadAudio(blob, userId, entryId)  │
│     └─> Returns: { audioPath, audioSizeBytes }              │
│  3. entriesService.addEntry() con audioPath + metadata       │
│  4. POST /api/v1/entries → Backend                          │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│ Supabase Storage (Private Bucket "audio")                   │
│                                                              │
│ Path: audio/{userId}/{entryId}.webm                         │
│ Access: Requires valid JWT token + RLS policy check         │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│ PostgreSQL (Database)                                        │
│                                                              │
│ Entry table:                                                 │
│ - id, userId, text, createdAt                               │
│ - audioPath: "userId/entryId.webm"                          │
│ - audioMime: "audio/webm"                                   │
│ - audioDurationMs: 45000                                    │
│ - audioSizeBytes: 125000                                    │
└─────────────────────────────────────────────────────────────┘
```

### 2. Playback Flow

```
┌────────────────────────────────────────────────────────────┐
│ Frontend (EntryCard)                                        │
│                                                            │
│  1. componentDidMount() → loadSignedUrl(audioPath)         │
│  2. supabaseStorage.getSignedUrl(audioPath, 86400)         │
│     └─> Returns: signed URL (valid 24h)                   │
│  3. <audio src={signedUrl} controls />                     │
│  4. Schedule refresh in 23h (warn at 1h remaining)         │
└────────────────────────────────────────────────────────────┘
         │
         ▼
┌────────────────────────────────────────────────────────────┐
│ Supabase Storage (Private Bucket)                          │
│                                                            │
│ Signed URL validation:                                     │
│ - Token signature valid?                                   │
│ - URL timestamp not expired?                               │
│ - RLS policy allows this user? (audioPath matches userId)  │
│ - Return file stream via HTTPS                            │
└────────────────────────────────────────────────────────────┘
         │
         ▼
┌────────────────────────────────────────────────────────────┐
│ Browser HTML5 <audio>                                      │
│                                                            │
│ - Play controls                                            │
│ - Duration: entry.audioDurationMs                         │
│ - Size: entry.audioSizeBytes                              │
│ - Mime: entry.audioMime                                   │
└────────────────────────────────────────────────────────────┘
```

---

## Implementation Details

### Files Involved

1. **[app/src/features/entries/hooks/useEntryForm.ts](../app/src/features/entries/hooks/useEntryForm.ts)**
   - Orchestrates upload: calls `supabaseStorage.uploadAudio()`
   - Handles userId extraction from Supabase session
   - Generates entry UUID for path

2. **[app/src/shared/lib/supabaseStorage.ts](../app/src/shared/lib/supabaseStorage.ts)**
   - `uploadAudio(blob, userId, entryId, mimeType)` → Uploads to `audio/{userId}/{entryId}.ext`
   - `getSignedUrl(audioPath, expiresIn=86400)` → Generates 24h signed URL
   - `deleteAudio(audioPath)` → Removes file on entry deletion

3. **[app/src/features/entries/components/EntryCard.tsx](../app/src/features/entries/components/EntryCard.tsx)**
   - Displays audio player with signed URL
   - Shows metadata: duration, file size, MIME type
   - Handles URL expiry warning (1h before expiration)
   - Implements refresh mechanism for expired URLs

4. **[app/src/features/entries/services/entriesService.ts](../app/src/features/entries/services/entriesService.ts)**
   - `addEntry(text, audioPath, audioMime, audioDurationMs, audioSizeBytes)`
   - Sends metadata to backend

5. **Backend: [api/Memoa.WebApi/Memoa.Application/Handlers/CreateEntryRequestHandler.cs](../api/Memoa.WebApi/Memoa.Application/Handlers/CreateEntryRequestHandler.cs)**
   - Validates JWT claims (extracts userId from `sub`)
   - Validates text is not empty
   - Saves Entry with all metadata

### Upload Path Pattern

```
audio/{userId}/{entryId}.webm
```

- **{userId}**: From JWT claim `sub` (Supabase format: UUID)
- **{entryId}**: Generated by FE using `uuid v4`
- **Extension**: Auto-detected from MIME type (webm, mp4, wav)

**Example**: `audio/550e8400-e29b-41d4-a716-446655440000/f47ac10b-58cc-4372-a567-0e02b2c3d479.webm`

### Error Handling

#### Upload Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `Errore caricamento audio: Upload fallito: Unauthorized` | Invalid or expired JWT | User must be logged in; token auto-refresh via Supabase |
| `Errore caricamento audio: Upload fallito: Access Denied` | Trying to upload to wrong userId path | RLS policy enforces userId ownership |
| `Errore caricamento audio: Upload fallito: No such bucket` | Bucket `audio` doesn't exist | Must be created in Supabase Storage |
| Network timeout | Poor connection or large file | Auto-retry via `useEntryForm` error handling |

#### Playback Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `Errore accesso audio: Errore generazione URL: ...` | File doesn't exist or RLS deny | Check audioPath in DB matches file path |
| Audio tag: "NotAllowedError" | CORS or invalid URL | Ensure signed URL is valid and bucket is configured |
| Audio tag: "NotSupportedError" | Browser doesn't support codec | Fallback to mp4 or wav format |

---

## RLS Policies (Supabase Configuration)

### Required Setup

The `audio` bucket must have RLS policies that enforce userId-based access:

```sql
-- Policy 1: Users can upload to their own folder
CREATE POLICY "Users can upload to own folder"
ON storage.objects
FOR INSERT
WITH CHECK (
  auth.uid()::text = (storage.foldername(name))[1]
);

-- Policy 2: Users can read their own files (generates signed URL)
CREATE POLICY "Users can read own files"
ON storage.objects
FOR SELECT
USING (
  auth.uid()::text = (storage.foldername(name))[1]
);

-- Policy 3: Users can delete their own files
CREATE POLICY "Users can delete own files"
ON storage.objects
FOR DELETE
USING (
  auth.uid()::text = (storage.foldername(name))[1]
);
```

**What this means**:
- ✅ User A can upload to `audio/uuid-A/*`
- ❌ User A cannot upload to `audio/uuid-B/*` → "permission denied"
- ✅ Signed URLs only work if file path matches userId
- ✅ Files are truly private; direct URL access is denied

---

## Security Considerations

### 1. Private Bucket Access
- No public URL access to files
- Signed URLs are time-limited (24 hours)
- JWT token required for upload/download operations

### 2. Path Validation
- Frontend generates `userId/entryId` path
- Backend stores path in DB
- RLS policies prevent cross-user access

### 3. File Integrity
- File size tracked in metadata
- MIME type validated (audio/webm, audio/mp4, audio/wav)
- Duration prevents suspiciously long recordings (max 5min enforced by recorder)

### 4. Token Refresh
- Supabase JWT auto-refresh on expiry
- EntryCard warns when signed URL expires (1h before)
- User can refresh page to get new signed URL

---

## User Experience Flow

### Upload Journey

```
1. User clicks "Registra"
   └─> useAudioRecorder starts listening to mic

2. Recording in progress
   └─> useAudioRecorder shows timer (max 5 min)

3. User clicks "Stop"
   └─> Blob + metadata returned

4. User fills text + clicks "Salva"
   └─> useEntryForm.submitEntry():
       a. Validates text
       b. Uploads blob → Supabase Storage
       c. Gets audioPath back
       d. POST /api/v1/entries with audioPath + metadata
       e. Entry saved in DB

5. Loading state during upload
   └─> Show spinner, disable submit button

6. Success
   └─> Reset form, refresh entries list, show toast

7. Error during upload
   └─> Show error message, allow retry, delete file from storage
```

### Playback Journey

```
1. Entries page loads
   └─> EntryCard mounts for each entry

2. If entry.audioPath exists
   └─> loadSignedUrl():
       a. Call getSignedUrl(audioPath)
       b. Display loading spinner

3. Signed URL generated
   └─> <audio src={signedUrl} controls /> appears
   └─> Show metadata: duration, size, MIME type

4. User plays audio
   └─> Browser renders play controls

5. If signed URL expires (>24h)
   └─> Show warning: "Link in scadenza. Ricarica la pagina."
   └─> User refreshes page → new signed URL generated

6. If file was deleted (orphaned)
   └─> Error message: "Errore accesso audio: ..."
   └─> User can only delete entry from UI
```

---

## Testing Checklist

- [ ] **Upload to correct path**: Verify files appear in `audio/{userId}/{entryId}.webm`
- [ ] **Wrong userId path denied**: Attempt upload to different userId → "permission denied"
- [ ] **Playback with signed URL**: Audio plays correctly with time-limited URL
- [ ] **URL expiry warning**: Message appears 1h before expiration
- [ ] **File deletion**: Delete entry → file removed from storage + DB
- [ ] **Metadata accuracy**: Duration, size, MIME type match recording
- [ ] **Error handling**: Network error during upload → retry available
- [ ] **Error handling**: File deleted externally → error message shown
- [ ] **Cross-browser**: Works in Chrome, Firefox, Safari (codec support varies)

---

## Environment Setup

Required Supabase configuration in `.env.local`:

```env
VITE_SUPABASE_URL=https://eeckgxnhwqhzqwybzqvm.supabase.co
VITE_SUPABASE_ANON_KEY=sb_publishable_lpb2Unr4vIe0G3FhpdBNgA_cWIoOCZS
VITE_SUPABASE_BUCKET_URL=https://eeckgxnhwqhzqwybzqvm.storage.supabase.co/storage/v1/s3
```

- Bucket must be named `audio`
- Bucket must be **PRIVATE** (not public)
- RLS policies must be enabled
- Auth method: Supabase JWT (via `supabase.auth.*`)

---

## Future Enhancements

1. **Retry logic**: Auto-retry upload on network failure
2. **Compression**: Pre-compress large recordings before upload
3. **Transcription**: Send audio to backend for speech-to-text
4. **CDN caching**: Cache signed URLs for faster playback
5. **Analytics**: Track which entries have audio, average file size
6. **Streaming upload**: For large files, use multipart upload
7. **URL refresh**: Auto-refresh signed URL before expiry (instead of warning)
