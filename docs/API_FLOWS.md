# API Integration e Flussi - Memoa FE

## Indice

1. [Architettura API](#architettura-api)
2. [Client HTTP](#client-http)
3. [Endpoints Disponibili](#endpoints-disponibili)
4. [Flussi Dettagliati](#flussi-dettagliati)
5. [Autenticazione API](#autenticazione-api)
6. [Error Handling API](#error-handling-api)
7. [Supabase Integration](#supabase-integration)
8. [Debugging e Logging](#debugging-e-logging)

---

## Architettura API

### Layer Stack

```
┌──────────────────────────────────────────┐
│      React Components                    │
│  (EntryForm, HomePage, LoginPage)        │
└────────────────┬─────────────────────────┘
                 │
┌────────────────▼─────────────────────────┐
│      Custom Hooks                        │
│  (useEntries, useEntryForm, useUser)     │
└────────────────┬─────────────────────────┘
                 │
┌────────────────▼─────────────────────────┐
│      Services                            │
│  (entriesService, supabaseStorage)       │
└────────────────┬─────────────────────────┘
                 │
┌────────────────▼─────────────────────────┐
│      API Client                          │
│  (apiClient, fetchWithAuth)              │
└────────────────┬─────────────────────────┘
                 │
┌────────────────▼─────────────────────────┐
│      External Services                   │
│  (Backend API, Supabase Storage)         │
└──────────────────────────────────────────┘
```

### Flow Richiesta API

```
1. Component User Action
   ↓
2. Hook Handler (e.g., submitEntry)
   ↓
3. Service Method (e.g., addEntry)
   ↓
4. API Client Request (e.g., post)
   ↓
5. Get Auth Token (from Supabase)
   ↓
6. Build Headers (Content-Type, Authorization)
   ↓
7. Fetch HTTP Request
   ↓
8. Backend Processing
   ↓
9. HTTP Response (200, 4xx, 5xx)
   ↓
10. Parse Response / Error
    ↓
11. Return ApiResponse<T>
    ↓
12. Hook Update State
    ↓
13. Component Re-render
    ↓
14. UI Updated
```

---

## Client HTTP

### `apiClient.ts` - Generic HTTP Client

**Ruolo**: Centralizzare logica HTTP con autenticazione automatica

**Classe**: `ApiClient` (singleton)

#### Metodi

```typescript
class ApiClient {
  // GET request
  get<T>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>>
  
  // POST request
  post<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<ApiResponse<T>>
  
  // PUT request
  put<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<ApiResponse<T>>
  
  // DELETE request
  delete<T>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>>
  
  // PATCH request
  patch<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<ApiResponse<T>>
}
```

#### Implementazione Dettagliata

```typescript
class ApiClient {
  private async getAuthToken(): Promise<string | null> {
    // Estrae JWT token da Supabase session
    const { data: { session } } = await supabase.auth.getSession();
    return session?.access_token || null;
  }

  private async handleUnauthorized(): Promise<void> {
    // Logout e reindirizza a login su 401
    try {
      await supabase.auth.signOut();
      window.location.href = '/login';
    } catch (error) {
      window.location.href = '/login';
    }
  }

  private buildHeaders(token: string | null): HeadersInit {
    // Costruisce headers con auth
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    return headers;
  }

  private async parseErrorResponse(response: Response): Promise<string> {
    // Parsa errore da ProblemDetails o response
    try {
      const problem: ProblemDetails = await response.json();
      return problem.detail || problem.title || `HTTP ${response.status}`;
    } catch {
      return `HTTP ${response.status}: ${response.statusText}`;
    }
  }

  async request<T = unknown>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<ApiResponse<T>> {
    try {
      const token = await this.getAuthToken();
      const url = `${API_URL}${endpoint}`;
      const headers = this.buildHeaders(token);

      const response = await fetch(url, {
        ...options,
        headers,
      });

      // Handle 401 Unauthorized
      if (response.status === 401) {
        await this.handleUnauthorized();
        return { error: 'Unauthorized', status: 401 };
      }

      // Handle 204 No Content (successful delete)
      if (response.status === 204) {
        return { data: undefined as T, status: 204 };
      }

      // Handle error responses
      if (!response.ok) {
        const error = await this.parseErrorResponse(response);
        return { error, status: response.status };
      }

      // Parse successful response (wrapped in ApiSuccessResult<T>)
      const apiResult: ApiSuccessResult<T> = await response.json();

      if (apiResult.data !== undefined) {
        return { data: apiResult.data, status: response.status };
      }

      return {
        error: apiResult.resultDescription || 'No data returned',
        status: response.status,
      };
    } catch (error) {
      console.error('API request error:', error);
      return {
        error: error instanceof Error ? error.message : 'Unknown error',
        status: 0,
      };
    }
  }

  get<T>(endpoint: string, options?: RequestInit) {
    return this.request<T>(endpoint, { ...options, method: 'GET' });
  }

  post<T>(endpoint: string, body?: unknown, options?: RequestInit) {
    return this.request<T>(endpoint, {
      ...options,
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  put<T>(endpoint: string, body?: unknown, options?: RequestInit) {
    return this.request<T>(endpoint, {
      ...options,
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  delete<T>(endpoint: string, options?: RequestInit) {
    return this.request<T>(endpoint, { ...options, method: 'DELETE' });
  }

  patch<T>(endpoint: string, body?: unknown, options?: RequestInit) {
    return this.request<T>(endpoint, {
      ...options,
      method: 'PATCH',
      body: body ? JSON.stringify(body) : undefined,
    });
  }
}

export const apiClient = new ApiClient();
```

#### Utilizzo

```typescript
// GET
const response = await apiClient.get<UserDto>('/api/v1/user');
if (response.data) {
  console.log(response.data.email);
} else {
  console.error(response.error);
}

// POST
const response = await apiClient.post<Entry>('/api/v1/entries', {
  text: 'My note',
  audioPath: 'audio/user1/entry1.webm',
});

// DELETE
const response = await apiClient.delete('/api/v1/entries/123');

// PUT
const response = await apiClient.put<Entry>('/api/v1/entries/123', {
  text: 'Updated note',
});
```

### `entriesService.ts` - Domain-Specific Service

**Ruolo**: Wrappare API client con logica entries specifica

```typescript
export const entriesService = {
  /**
   * Fetch all entries for current user
   * GET /api/v1/entries
   */
  async getEntries(): Promise<ApiResponse<Entry[]>> {
    return apiClient.get<Entry[]>('/api/v1/entries');
  },

  /**
   * Create new entry
   * POST /api/v1/entries
   */
  async addEntry(
    text: string,
    audioPath?: string,
    audioMime?: string,
    audioDurationMs?: number,
    audioSizeBytes?: number
  ): Promise<ApiResponse<Entry>> {
    const request: CreateEntryRequest = {
      id: uuidv4(),
      text,
      createdAt: new Date().toISOString(),
      ...(audioPath && { audioPath, audioMime, audioDurationMs, audioSizeBytes }),
    };

    return apiClient.post<Entry>('/api/v1/entries', request);
  },

  /**
   * Delete entry
   * DELETE /api/v1/entries/{id}
   */
  async deleteEntry(id: string): Promise<ApiResponse<void>> {
    return apiClient.delete<void>(`/api/v1/entries/${id}`);
  },

  /**
   * Update entry
   * PUT /api/v1/entries/{id}
   */
  async updateEntry(id: string, text: string): Promise<ApiResponse<Entry>> {
    const request: UpdateEntryRequest = { text };
    return apiClient.put<Entry>(`/api/v1/entries/${id}`, request);
  },
};
```

---

## Endpoints Disponibili

### Autenticazione (Supabase)

**No explicit API endpoints** - Gestita da Supabase SDK

```typescript
// Magic Link Login
supabase.auth.signInWithOtp({
  email: 'user@example.com',
  options: { shouldCreateUser: true }
})

// Logout
supabase.auth.signOut()

// Get Session
supabase.auth.getSession()

// Auth State Changes
supabase.auth.onAuthStateChange(callback)
```

### Entries API

#### GET /api/v1/entries
**Descrizione**: Fetch lista note per utente corrente

**Autenticazione**: ✅ Required (Bearer token)

**Response**:
```typescript
{
  data?: Entry[],
  httpStatusCode: 200,
  resultCode: 0,
  resultDescription: "Success",
}
```

**Utilizzo**:
```typescript
const response = await entriesService.getEntries();
if (response.success && response.data) {
  // response.data: Entry[]
}
```

#### POST /api/v1/entries
**Descrizione**: Crea nuova nota

**Autenticazione**: ✅ Required (Bearer token)

**Request Body**:
```typescript
{
  id: string;              // UUID generato da client
  text: string;            // Contenuto nota (required)
  createdAt: string;       // ISO timestamp
  audioPath?: string;      // Path Supabase storage (optional)
  audioMime?: string;      // MIME type audio (optional)
  audioDurationMs?: number; // Durata audio (optional)
  audioSizeBytes?: number;  // Size audio bytes (optional)
}
```

**Response**:
```typescript
{
  data?: Entry,
  httpStatusCode: 201,
  resultCode: 0,
  resultDescription: "Created",
}
```

**Utilizzo**:
```typescript
const response = await entriesService.addEntry(
  'My note text',
  'audio/user1/entry1.webm',
  'audio/webm',
  15000,
  25000
);
if (response.success && response.data) {
  // response.data: Entry (creato)
}
```

#### DELETE /api/v1/entries/{id}
**Descrizione**: Elimina nota e audio associato

**Autenticazione**: ✅ Required (Bearer token)

**URL Parameters**:
```
{id}: string - UUID entry da eliminare
```

**Response**:
```typescript
{
  httpStatusCode: 204,
  resultCode: 0,
  resultDescription: "Deleted",
}
```

**Utilizzo**:
```typescript
const response = await entriesService.deleteEntry('entry-uuid');
if (response.success) {
  // Nota eliminata
}
```

#### PUT /api/v1/entries/{id}
**Descrizione**: Aggiorna nota (non ancora usato in UI)

**Autenticazione**: ✅ Required (Bearer token)

**Request Body**:
```typescript
{
  text: string; // Nuovo contenuto
}
```

**Response**:
```typescript
{
  data?: Entry,
  httpStatusCode: 200,
  resultCode: 0,
  resultDescription: "Updated",
}
```

### User API

#### GET /api/v1/user
**Descrizione**: Fetch dati utente corrente

**Autenticazione**: ✅ Required (Bearer token)

**Response**:
```typescript
{
  data?: {
    id: string;     // UUID utente
    email: string;  // Email autenticato
  },
  httpStatusCode: 200,
  resultCode: 0,
  resultDescription: "Success",
}
```

**Utilizzo**:
```typescript
const response = await apiClient.get<UserDto>('/api/v1/user');
if (response.data) {
  console.log(response.data.email, response.data.id);
}
```

---

## Flussi Dettagliati

### Flusso 1: Caricamento Pagina (HomePage)

```
1. HomePage Mount
   └─ useAuth().accessToken (already have from AuthProvider)
   
2. Parallelo:
   a) useUser.fetchUser()
      └─ apiClient.get('/api/v1/user')
         └─ Authorization: Bearer {token}
         └─ Response: UserDto
         └─ setUser(userData)
   
   b) useEntries.loadEntries()
      └─ entriesService.getEntries()
         └─ apiClient.get('/api/v1/entries')
         └─ Authorization: Bearer {token}
         └─ Response: Entry[]
         └─ setEntries(entriesData)

3. UI Render
   ├─ UserInfo mostra email
   ├─ EntryForm ready per input
   └─ EntryList mostra note
```

**Timeline**:
```
T0: Mount
T1-100ms: Token disponibile (da AuthProvider)
T101-500ms: User fetch + Entries fetch (parallelo)
T501+: UI renderizza con dati
```

**Error Handling**:
```
- User fetch fail → useUser.error
- Entries fetch fail → useEntries.error
- Mostrati via ErrorMessage component
```

### Flusso 2: Creazione Nota

```
1. Utente digita testo + opzionale registra audio
   ├─ EntryForm.content aggiornato
   └─ useAudioRecorder.blob aggiornato

2. Utente clicca "Salva"
   └─ HomePage.handleAddEntry(e)
      └─ useEntryForm.submitEntry()

3. useEntryForm.submitEntry()
   a) Validate content
      └─ if !content.trim() → setValidationError
   
   b) Get userId da sessione
      └─ supabase.auth.getSession()
   
   c) Se audio presente:
      └─ supabaseStorage.uploadAudio()
         └─ POST /audio/{userId}/{entryId}.webm
         └─ Binary file upload
         └─ Response: { audioPath, audioSizeBytes }
   
   d) Crea entry con metadati
      └─ entriesService.addEntry(text, audioPath, ...)
         └─ apiClient.post('/api/v1/entries', request)
         └─ Authorization: Bearer {token}
         └─ Response: Entry (creata)
   
   e) Success:
      └─ window.location.reload()
      └─ HomePage rimonta
      └─ useEntries ricarica lista
```

**Timeline**:
```
T0: Form submit
T1-50ms: Validation
T51-100ms: Get session
T101-2000ms: Audio upload (if present)
T2001-2500ms: Entry create API
T2501-3000ms: Page reload
T3001+: UI updated
```

**Error Handling**:
```
- Validation error → useEntryForm.validationError (mostra in form)
- Audio upload fail → catch block → setSubmitError
- Entry create fail → catch block → setSubmitError
- UI mostra submitError in EntryForm
```

### Flusso 3: Visualizzazione Audio

```
1. EntryCard Mount
   ├─ entry.audioPath disponibile
   └─ useEffect → fetch signed URL

2. supabaseStorage.getSignedUrl(audioPath)
   └─ GET /storage/v1/object/sign/{audioPath}?download=true
   └─ Response: signed URL (valido 1 ora)
   └─ setAudioSignedUrl(url)

3. UI Render
   ├─ audioLoading = false
   └─ <audio src={audioSignedUrl}>
      └─ Utente può play/pause/download
```

**Timeline**:
```
T0: Mount
T1-50ms: audioLoading = true
T51-500ms: Fetch signed URL
T501+: UI renderizza audio player
```

**Error Handling**:
```
- Fetch fail → audioError stato
- UI mostra audioError messaggio
```

### Flusso 4: Eliminazione Nota

```
1. Utente clicca delete button su EntryCard
   └─ HomePage.handleDeleteEntry(id)

2. useEntries.deleteEntry(id)
   └─ entriesService.deleteEntry(id)
      └─ apiClient.delete(`/api/v1/entries/${id}`)
      └─ Authorization: Bearer {token}
      └─ Backend elimina entry + audio
      └─ Response: 204 No Content

3. Frontend update:
   └─ setEntries(prev => prev.filter(e => e.id !== id))
   └─ Nota scompare da lista

4. UI Update:
   └─ EntryList re-render
   └─ Meno card disponibili
```

**Timeline**:
```
T0: Delete click
T1-100ms: API call
T101-500ms: Backend processing
T501+: Frontend update + re-render
```

**Error Handling**:
```
- API fail → useEntries.error
- Nota rimane in lista
- ErrorMessage mostra errore
```

### Flusso 5: Logout

```
1. Utente clicca bottone Logout in Header
   └─ Header.handleLogout()

2. useAuth.logout()
   └─ useAuthSessionManagement.logout()
      └─ supabase.auth.signOut()
         └─ Clear local session storage
         └─ onAuthStateChange('SIGNED_OUT') trigger

3. useAuthSessionManagement.onAuthStateChange
   └─ isAuthenticated = false
   └─ accessToken = null

4. Router rileva cambio
   └─ isAuthenticated = false
   └─ Reindirizza a /login

5. LoginPage Mount
   └─ User vede form email
```

**Timeline**:
```
T0: Logout click
T1-50ms: signOut() called
T51-500ms: Session cleared
T501-1000ms: Auth state change detected
T1001+: Router redirect
T1100+: LoginPage render
```

**Error Handling**:
```
- signOut() fail → Forza redirect a /login
- Fallback redirect on error
```

---

## Autenticazione API

### Header Authorization

**Pattern**: `Authorization: Bearer {JWT_TOKEN}`

**Origine Token**:
```typescript
const { data: { session } } = await supabase.auth.getSession();
const token = session?.access_token; // JWT token

// Header
'Authorization': `Bearer ${token}`
```

**Token Scadenza**:
- Durata: Solitamente 1 ora
- Refresh: Supabase refresh automatico se expired
- Fallback: 401 → Logout → Login

### Flow Autorizzazione

```
1. Login Riuscito
   └─ Supabase emette JWT token (exp: now + 1h)
   
2. apiClient estrae token
   └─ supabase.auth.getSession()
   └─ token = session?.access_token
   
3. Ogni richiesta API
   └─ ADD header: Authorization: Bearer {token}
   
4. Backend verifica JWT
   ├─ Token valido → Processa richiesta
   ├─ Token scaduto → HTTP 401
   └─ Token invalido → HTTP 401

5. Frontend riceve 401
   └─ apiClient.handleUnauthorized()
      └─ supabase.auth.signOut()
      └─ window.location.href = '/login'
      └─ Forza logout e redirect
```

### Scoping Token

**Supabase JWT Claims** (tipicamente incluso):
```json
{
  "sub": "user-uuid",
  "email": "user@example.com",
  "iat": 1234567890,
  "exp": 1234571490,
  "aud": "authenticated"
}
```

**Backend Utilizza**:
- `sub` → User ID per filtrare entries
- `email` → Identificare utente
- `exp` → Validare scadenza

---

## Error Handling API

### Error Response Format

**Backend risponde con ProblemDetails** (RFC 7231):
```typescript
{
  "type": "https://example.com/validation-error",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Email is required",
  "instance": "/api/v1/entries",
  "errors": {
    "email": ["Email is required"]
  }
}
```

### apiClient Error Parsing

```typescript
private async parseErrorResponse(response: Response): Promise<string> {
  try {
    const problem: ProblemDetails = await response.json();
    return problem.detail || problem.title || `HTTP ${response.status}`;
  } catch {
    return `HTTP ${response.status}: ${response.statusText}`;
  }
}
```

### HTTP Status Codes Gestiti

| Status | Gestione | Azione |
|---|---|---|
| 200 OK | ✅ Successo | Parse data |
| 201 Created | ✅ Creato | Parse data |
| 204 No Content | ✅ Successo (delete) | Return undefined |
| 400 Bad Request | ❌ Validazione | Parse error, mostra UI |
| 401 Unauthorized | ❌ Auth fail | Logout + redirect |
| 403 Forbidden | ❌ Permessi | Parse error, mostra UI |
| 404 Not Found | ❌ Risorsa | Parse error, mostra UI |
| 500+ Server Error | ❌ Backend | Parse error, mostra UI |

### Hook Error Handling

```typescript
// useEntries
const deleteEntry = async (id: string) => {
  setError(null);
  try {
    const response = await entriesService.deleteEntry(id);
    if (response.success) {
      setEntries(prev => prev.filter(e => e.id !== id));
      return true;
    } else {
      setError(response.error || 'Errore eliminazione');
      return false;
    }
  } catch (err) {
    setError('Errore imprevisto');
    return false;
  }
};
```

### Component Error Display

```typescript
// HomePage
{error && <ErrorMessage message={error} />}

// ErrorMessage component
export const ErrorMessage = ({ message }: Props) => (
  <div className="p-4 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded-lg">
    {message}
  </div>
);
```

---

## Supabase Integration

### Servizi Supabase Usati

#### 1. Supabase Auth (Passwordless)

```typescript
// Magic link login
await supabase.auth.signInWithOtp({
  email: 'user@example.com',
  options: { shouldCreateUser: true }
});

// Get session
const { data: { session } } = await supabase.auth.getSession();

// Logout
await supabase.auth.signOut();

// Listen to auth state
supabase.auth.onAuthStateChange((event, session) => {
  // event: 'SIGNED_IN', 'SIGNED_OUT', 'INITIAL_SESSION'
  // session: Session | null
});
```

#### 2. Supabase Storage

```typescript
// Upload file
await supabase.storage
  .from('audio')
  .upload('user-id/entry-id.webm', file);

// Get signed URL
const { data: { signedUrl } } = await supabase.storage
  .from('audio')
  .createSignedUrl('user-id/entry-id.webm', 3600); // 1 hour

// Delete file
await supabase.storage
  .from('audio')
  .remove(['user-id/entry-id.webm']);
```

### Bucket Structure

```
Bucket: "audio"
├── user-1/
│   ├── entry-1.webm
│   ├── entry-2.webm
│   └── ...
├── user-2/
│   ├── entry-1.mp4
│   └── ...
└── ...
```

### Security Policies

**Policies** (RLS - Row Level Security):
```sql
-- Utenti possono accedere solo ai loro file
CREATE POLICY "Users can access their own audio"
ON storage.objects
FOR SELECT
TO authenticated
USING (bucket_id = 'audio' AND auth.uid()::text = (storage.foldername(name))[1]);
```

**Resultado**: 
- Ogni utente vede solo file nella sua cartella
- Nessun accesso cross-user

---

## Debugging e Logging

### Console Logging

#### API Client Logging

```typescript
// apiClient.ts - Aggiungere logging
private async request<T>(endpoint: string, options: RequestInit = {}) {
  console.log('[API] Request:', endpoint, options.method || 'GET');
  
  try {
    const token = await this.getAuthToken();
    console.log('[API] Token:', token ? '✓ Present' : '✗ Missing');
    
    const response = await fetch(url, { ...options, headers });
    
    console.log('[API] Response:', response.status, response.statusText);
    
    // ... handle response
    
    return { data, status: response.status };
  } catch (error) {
    console.error('[API] Error:', error);
    return { error: error.message };
  }
}
```

#### Service Logging

```typescript
// entriesService.ts
async getEntries() {
  console.log('[Service] Fetching entries...');
  const response = await apiClient.get<Entry[]>('/api/v1/entries');
  console.log('[Service] Entries fetched:', response.data?.length);
  return response;
}
```

#### Hook Logging

```typescript
// useEntries.ts
useEffect(() => {
  console.log('[Hook] useEntries mounting, loading entries...');
  loadEntries();
  
  return () => {
    console.log('[Hook] useEntries unmounting');
  };
}, []);
```

### Browser DevTools

#### Network Tab
1. Apri DevTools → Network
2. Filtra per XHR/Fetch
3. Clicca su richiesta
4. Ispeziona:
   - **Headers**: Authorization, Content-Type
   - **Payload**: Request body JSON
   - **Response**: Response body JSON
   - **Timing**: Durata richiesta

#### Application Tab
1. Apri DevTools → Application
2. **Cookies**: Supabase session cookie
3. **LocalStorage**:
   - `sb-{project-id}-auth-token` → JWT token
   - `theme` → Tema preferenza
4. **IndexedDB**: Dati persistiti Supabase

#### Console
```javascript
// Ottieni sessione corrente
const { data: { session } } = await supabase.auth.getSession();
console.log(session);

// Ottieni token
console.log(session?.access_token);

// Verifica URL API
console.log(import.meta.env.VITE_API_URL);
```

### Debugging Autenticazione

```typescript
// In component o console
import { supabase } from './shared/lib/supabaseClient';

// Check auth state
const { data: { user } } = await supabase.auth.getUser();
console.log('Current user:', user);

// Check session
const { data: { session } } = await supabase.auth.getSession();
console.log('Session:', session);

// Listen for changes
supabase.auth.onAuthStateChange((event, session) => {
  console.log('Auth event:', event, session?.user?.email);
});
```

### Debugging Storage

```typescript
// Check file exists
const { data, error } = await supabase.storage
  .from('audio')
  .list('user-id');

console.log('Files:', data);
if (error) console.error('Error:', error);

// Get signed URL
const { data: { signedUrl }, error } = 
  await supabase.storage
    .from('audio')
    .createSignedUrl('user-id/entry-id.webm', 3600);

console.log('URL:', signedUrl);
```

### Debugging Applicazione

```bash
# Avvia dev server con logging dettagliato
npm run dev

# In browser console
// Abilita detailed logging
localStorage.setItem('DEBUG', '*');

// Disabilita
localStorage.removeItem('DEBUG');
```

### Log Levels

| Level | Quando | Esempio |
|---|---|---|
| **Error** | Fallimento | API fail, user action fail |
| **Warn** | Non ideale | Missing data, deprecated API |
| **Info** | Evento importante | Login, logout, page load |
| **Debug** | Dettagli | Function call, state change |
| **Trace** | Tutto | Every variable assignment |

### Monitoraggio Produzione

**Potenziali Integrazioni**:
- **Sentry**: Error tracking
- **LogRocket**: Session replay
- **DataDog**: Performance monitoring
- **Google Analytics**: User behavior

---

## Summary API Integration

✅ **Client HTTP Centralizzato**
- `apiClient` gestisce headers, auth, errori

✅ **Service Layer**
- `entriesService` fornisce operazioni note
- Separa logica business da HTTP

✅ **Autenticazione Automatica**
- JWT token estratto da Supabase
- Added ad ogni richiesta

✅ **Error Handling Multi-Layer**
- Network errors
- API errors
- Business logic validation

✅ **Supabase Integration**
- Auth (passwordless magic link)
- Storage (audio files)
- Session management

✅ **Debugging Support**
- Console logging strutturato
- DevTools inspection
- Error tracing

L'architettura API è **scalabile**, **maintainable**, e **type-safe** con TypeScript.
