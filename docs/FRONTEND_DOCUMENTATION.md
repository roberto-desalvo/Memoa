# Documentazione Frontend - Memoa

## Indice

1. [Panoramica del Progetto](#panoramica-del-progetto)
2. [Stack Tecnologico](#stack-tecnologico)
3. [Struttura del Progetto](#struttura-del-progetto)
4. [Componenti React](#componenti-react)
5. [Hook Personalizzati](#hook-personalizzati)
6. [Servizi API](#servizi-api)
7. [Gestione dello Stato](#gestione-dello-stato)
8. [Autenticazione](#autenticazione)
9. [Storage e Gestione File Audio](#storage-e-gestione-file-audio)
10. [Tema e UI](#tema-e-ui)
11. [Flusso dell'Applicazione](#flusso-dellapplicazione)

---

## Panoramica del Progetto

**Memoa** è un'applicazione web per la creazione e la gestione di note personali con supporto per allegati audio. L'applicazione è costruita con React e TypeScript, utilizza Supabase per l'autenticazione e lo storage, e comunica con un backend .NET tramite API REST.

### Caratteristiche Principali

- **Autenticazione Magica**: Accesso tramite link email (passwordless)
- **Note Testuali**: Creazione, visualizzazione ed eliminazione di note
- **Allegati Audio**: Registrazione e archiviazione di file audio associati alle note
- **Interfaccia Responsiva**: Design adattabile a dispositivi mobile e desktop
- **Tema Scuro/Chiaro**: Supporto per preferenze di tema dell'utente

---

## Stack Tecnologico

### Dipendenze Principali

| Dipendenza | Versione | Utilizzo |
|---|---|---|
| **React** | 19.2.0 | Framework UI |
| **React Router DOM** | 7.12.0 | Navigazione e routing |
| **TypeScript** | ~5.9.3 | Type-safe development |
| **Vite** | 7.2.4 | Build tool e dev server |
| **Tailwind CSS** | 3.4.19 | Utility-first styling |
| **Supabase** | 2.90.1 | Autenticazione e storage |
| **React Icons** | 5.5.0 | Icone UI |
| **UUID** | 9.0.1 | Generazione ID univoci |

### Tools di Sviluppo

- **ESLint**: Linting e code quality
- **PostCSS + Autoprefixer**: CSS processing
- **TypeScript Compiler**: Type checking

---

## Struttura del Progetto

```
src/
├── App.tsx                     # Componente root (legacy)
├── main.tsx                    # Entry point dell'applicazione
├── index.css                   # Stili globali
├── app/
│   └── router.tsx             # Configurazione routing
├── features/
│   ├── auth/                  # Feature autenticazione
│   │   ├── AuthProvider.tsx   # Context e provider auth
│   │   ├── LoginPage.tsx      # Pagina login
│   │   └── components/
│   │       ├── LoginForm.tsx  # Form login
│   │       └── LinkSentMessage.tsx # Messaggio conferma
│   └── entries/               # Feature note
│       ├── HomePage.tsx       # Pagina principale
│       └── components/
│           ├── EntryCard.tsx       # Card singola nota
│           ├── EntryForm.tsx       # Form creazione nota
│           ├── EntryList.tsx       # Lista note
│           ├── ErrorMessage.tsx    # Messaggio errore
│           ├── UserInfo.tsx        # Informazioni utente
│           └── UserLoadingState.tsx # Stato loading utente
└── shared/
    ├── Header.tsx             # Header navigazione
    ├── ThemeProvider.tsx      # Context tema chiaro/scuro
    ├── api/
    │   ├── entriesService.ts  # Servizio API entries
    │   └── types.ts           # Tipi TypeScript comuni
    ├── hooks/
    │   ├── useAudioRecorder.ts      # Hook registrazione audio
    │   ├── useAuthLogic.ts          # Logica autenticazione
    │   ├── useAuthSessionManagement.ts # Gestione sessione
    │   ├── useEntries.ts            # Gestione liste note
    │   ├── useEntryForm.ts          # Logica form note
    │   ├── useIsAuthenticated.ts    # Check autenticazione
    │   └── useUser.ts               # Dati utente
    └── lib/
        ├── apiClient.ts       # Client HTTP generico
        ├── supabaseClient.ts  # Client Supabase
        └── supabaseStorage.ts # Gestione storage audio
```

---

## Componenti React

### App-Level Components

#### `main.tsx`
**Ruolo**: Entry point dell'applicazione
- Monta l'app React nel DOM
- Avvolge l'app con provider (ThemeProvider, BrowserRouter, AuthProvider)
- Stabilisce il contesto globale per l'intera applicazione

```tsx
// Provider stack:
ThemeProvider → BrowserRouter → AuthProvider → Router
```

#### `router.tsx`
**Ruolo**: Configurazione routing centralizzata
- Definisce le rotte dell'applicazione
- Gestisce layout radice con `RootLayout`
- Implementa protezione route basata su autenticazione
- Mostra loading durante verifica sessione

**Rotte**:
- `/` → HomePage (solo autenticati)
- `/login` → LoginPage (solo non autenticati)
- Redirect automatico per route non riconosciute

---

### Feature: Autenticazione (auth/)

#### `AuthProvider.tsx`
**Ruolo**: Context provider per stato autenticazione globale

**Interfaccia**:
```typescript
interface AuthContextType {
  isAuthenticated: boolean;   // Utente ha sessione valida
  accessToken: string | null; // JWT token Supabase
  isLoading: boolean;         // Caricamento sessione
  logout: () => void;         // Funzione logout
}
```

**Utilizzo**: `const { isAuthenticated, accessToken, logout } = useAuth()`

**Flusso**:
1. Legge `useAuthSessionManagement()` per stato sessione
2. Espone context per consumatori
3. Mostra loading durante verifica iniziale

#### `LoginPage.tsx`
**Ruolo**: Pagina login (passwordless)

**Componenti**:
- Utilizza `useAuthLogic()` per logica form
- Mostra `LoginForm` per inserimento email
- Dopo invio, mostra `LinkSentMessage` con istruzioni

**UX Flow**:
```
1. Utente inserisce email
2. Click su "Invia Link"
3. useAuthLogic invia OTP via Supabase
4. Mostra messaggio "Controlla email"
5. Utente clicca link email
6. Supabase autentica la sessione
7. Router reindirizza a HomePage
```

#### `components/LoginForm.tsx`
**Ruolo**: Form inserimento email

**Props**:
```typescript
interface LoginFormProps {
  email: string;
  onEmailChange: (value: string) => void;
  loading: boolean;           // Stato submit
  error: string;             // Messaggio errore
  onSubmit: (e: FormEvent) => void;
}
```

#### `components/LinkSentMessage.tsx`
**Ruolo**: Messaggio conferma dopo invio link

**Props**:
```typescript
interface LinkSentMessageProps {
  email: string;
  onTryAgain: () => void;    // Reset form per nuovo tentativo
}
```

---

### Feature: Note (entries/)

#### `HomePage.tsx`
**Ruolo**: Pagina principale - hub della gestione note

**Responsabilità**:
1. Carica e mostra lista note (via `useEntries`)
2. Mostra dati utente corrente (via `useUser`)
3. Gestisce form creazione nota (via `useEntryForm`)
4. Gestisce eliminazione note
5. Compone sottoinsiemi di componenti

**Flusso Dati**:
```
HomePage
├── UserInfo (da useUser)
├── EntryForm (da useEntryForm)
│   └── useAudioRecorder (per registrazione audio)
└── EntryList
    └── [EntryCard × N] (da useEntries)
```

**Gestione Audio**:
- `handleAudioBlob()`: Riceve audio da recorder
- `handleAudioClear()`: Pulisce audio da form
- Automaticamente incluso nel submit nota

#### `components/EntryForm.tsx`
**Ruolo**: Form creazione note con supporto audio

**Props**:
```typescript
interface EntryFormProps {
  content: string;
  onContentChange: (value: string) => void;
  validationError: string | null;
  onSubmit: (e: FormEvent) => void;
  onAudioBlob?: (blob: Blob, metadata: AudioMetadata) => void;
  onAudioClear?: () => void;
  hasAudio?: boolean;
  isSubmitting?: boolean;
  submitError?: string | null;
}
```

**Funzionalità**:
- Textarea per testo nota
- Bottone registrazione audio con timer
- Barra di progresso (300s max)
- Mostra errori validazione e submit
- Submit disabilitato durante registrazione/submit

#### `components/EntryCard.tsx`
**Ruolo**: Card singola nota - visualizzazione

**Features**:
- Titolo e testo nota troncato
- Visualizzazione data/ora formattata
- Audio player con signed URL da Supabase Storage
- Bottone eliminazione con callback
- Loading state durante caricamento audio
- Gestione errori accesso audio

#### `components/EntryList.tsx`
**Ruolo**: Lista note con paginazione lazy

**Props**:
```typescript
interface EntryListProps {
  entries: Entry[];
  loading: boolean;
  onDeleteEntry: (id: string) => void;
}
```

**Comportamento**:
- Infinite scroll (load more quando scrolli al fondo)
- Grid responsivo (1 col mobile, 2 tablet, 3 desktop)
- Carica 10 note per volta

#### `components/ErrorMessage.tsx`
**Ruolo**: Componente errore riutilizzabile

**Props**:
```typescript
interface ErrorMessageProps {
  message: string;
  onDismiss?: () => void;  // Opzionale
}
```

#### `components/UserInfo.tsx`
**Ruolo**: Visualizza email e ID utente corrente

#### `components/UserLoadingState.tsx`
**Ruolo**: Skeleton loading durante caricamento dati utente

---

### Componenti Condivisi (shared/)

#### `Header.tsx`
**Ruolo**: Header navigazione con azioni utente

**Elementi**:
- Logo "Memoa"
- Bottone toggle tema (dark/light)
- Bottone Home
- Bottone Logout
- Fixed positioning (top)

**Visibilità**: Solo per utenti autenticati

#### `ThemeProvider.tsx`
**Ruolo**: Context provider per tema scuro/chiaro

**Interfaccia**:
```typescript
interface ThemeContextType {
  theme: 'light' | 'dark';
  toggleTheme: () => void;
}
```

**Implementazione**:
- Legge tema da `localStorage` al mount
- Applica classe CSS a `<html>`
- Persiste preferenze utente
- Tailwind supporta `.dark:` per stili scuri

---

## Hook Personalizzati

### Hook Autenticazione

#### `useAuthLogic.ts`
**Scopo**: Logica form login (email + OTP)

**Interfaccia**:
```typescript
interface UseAuthLogicReturn {
  email: string;
  setEmail: (email: string) => void;
  loading: boolean;           // Invio in corso
  error: string;             // Messaggio errore
  linkSent: boolean;         // Link inviato con successo
  sendMagicLink: (e: FormEvent) => Promise<void>;
  resetForm: () => void;
}
```

**Flusso**:
1. Utente inserisce email
2. `sendMagicLink()` chiama `supabase.auth.signInWithOtp()`
3. Supabase invia email con link magico
4. Flag `linkSent = true` mostra messaggio conferma
5. Utente clicca link email
6. Supabase autentica sessione
7. `useAuthSessionManagement` rileva cambio auth
8. Router reindirizza a HomePage

#### `useAuthSessionManagement.ts`
**Scopo**: Gestione sessione e stato autenticazione globale

**Interfaccia**:
```typescript
interface UseAuthSessionManagementReturn {
  isAuthenticated: boolean;
  accessToken: string | null;
  isLoading: boolean;
  logout: () => Promise<void>;
}
```

**Funzionamento**:
1. Verifica sessione iniziale con `supabase.auth.getSession()`
2. Subscribe a `onAuthStateChange` per cambiamenti futuri
3. Traccia logout esplicito per evitare stati inconsistenti
4. Gestisce cleanup listener al unmount

**State Machine**:
```
[Loading] → [Authenticated + Token] 
         → [Unauthenticated + No Token]
         → (Login) → [Loading] → [Authenticated + Token]
         → (Logout) → [Loading] → [Unauthenticated]
```

#### `useIsAuthenticated.ts`
**Scopo**: Hook convenienza per check autenticazione

Alias per `useAuth().isAuthenticated`

### Hook Dati

#### `useUser.ts`
**Scopo**: Carica dati utente corrente dal backend

**Interfaccia**:
```typescript
return {
  user: UserDto | null;
  loading: boolean;
  error: string | null;
}
```

**Flusso**:
1. Dipende da `useAuth().accessToken`
2. Se token disponibile, GET `/api/v1/user`
3. Ritorna email e ID utente
4. Usa `apiClient` per richiesta autenticata

#### `useEntries.ts`
**Scopo**: Gestione liste note (fetch, add, delete)

**Interfaccia**:
```typescript
return {
  entries: Entry[];
  loading: boolean;
  error: string | null;
  addEntry: (text: string) => Promise<boolean>;
  deleteEntry: (id: string) => Promise<boolean>;
  loadEntries: () => Promise<void>;
}
```

**Operazioni**:
- **loadEntries()**: GET lista note, triggerato a mount
- **addEntry()**: POST nota, aggiunge in testa lista
- **deleteEntry()**: DELETE nota, rimuove da lista

#### `useEntryForm.ts`
**Scopo**: Gestione logica form creazione nota

**Interfaccia**:
```typescript
interface EntryFormState {
  content: string;
  validationError: string | null;
  audioBlob: Blob | null;
  audioMetadata: AudioMetadata | null;
  isSubmitting: boolean;
  submitError: string | null;
}

interface UseEntryFormReturn extends EntryFormState {
  setContent: (content: string) => void;
  setValidationError: (error: string | null) => void;
  setAudioBlob: (blob: Blob, metadata: AudioMetadata) => void;
  clearAudio: () => void;
  validate: () => boolean;
  reset: () => void;
  submitEntry: () => Promise<boolean>;
}
```

**Funzionamento**:
1. Valida testo non vuoto
2. Se presente audio, upload a Supabase Storage
3. Crea entry con metadati audio
4. POST a `/api/v1/entries`
5. Reset form se successo

**Flusso Upload Audio**:
```
Audio Blob → Upload Storage → Signed URL → Entry Metadata → POST API
```

### Hook Audio

#### `useAudioRecorder.ts`
**Scopo**: Gestire registrazione audio dal microfono

**Interfaccia**:
```typescript
return {
  isRecording: boolean;
  elapsedSeconds: number;    // Tempo registrazione
  blob: Blob | null;         // Audio Blob
  error: string | null;
  audioMetadata: AudioMetadata | null;
  startRecording: () => Promise<void>;
  stopRecording: () => void;
  resetRecording: () => void;
}
```

**Vincoli**:
- Max 300 secondi (5 minuti)
- Auto-stop al raggiungimento max
- Supporta WebM, MP4, WAV

**Flusso**:
1. `startRecording()`: Richiede permesso microfono
2. Usa `navigator.mediaDevices.getUserMedia()`
3. MediaRecorder raccoglie chunk audio
4. Timer aggiorna UI ogni 100ms
5. `stopRecording()`: Finalizza blob e metadati
6. Cleanup: Interrompe stream e timer al unmount

---

## Servizi API

### `apiClient.ts`
**Ruolo**: Client HTTP generico con autenticazione automatica

**Interfaccia**:
```typescript
class ApiClient {
  get<T>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>>
  post<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<ApiResponse<T>>
  put<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<ApiResponse<T>>
  delete<T>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>>
  patch<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<ApiResponse<T>>
}
```

**Features**:
- Aggiunge automaticamente header `Authorization: Bearer {token}`
- Ritorna `ApiResponse<T>` con field `data` e `error`
- Gestisce 401 Unauthorized → Logout + Redirect
- Parla risposta errore da `ProblemDetails`
- Base URL da env var `VITE_API_URL`

**Utilizzo**:
```typescript
const response = await apiClient.get<UserDto>('/api/v1/user');
if (response.data) {
  // ...
} else {
  // response.error
}
```

### `entriesService.ts`
**Ruolo**: Servizio specifico per operazioni note

**Metodi**:
```typescript
getEntries(): Promise<ApiResponse<Entry[]>>
  // GET /api/v1/entries

addEntry(
  text: string,
  audioPath?: string,
  audioMime?: string,
  audioDurationMs?: number,
  audioSizeBytes?: number
): Promise<ApiResponse<Entry>>
  // POST /api/v1/entries

deleteEntry(id: string): Promise<ApiResponse<void>>
  // DELETE /api/v1/entries/{id}
```

**Headers Autenticazione**:
- Estrae token da `supabase.auth.getSession()`
- Usa `fetchWithAuth()` helper interno
- Fallback se token non disponibile

---

## Gestione dello Stato

### Pattern: Context + Hooks

L'applicazione usa **React Context + Custom Hooks** per gestione stato:

```
Global State (Context)
├── AuthContext (via AuthProvider)
│   └── useAuth() → isAuthenticated, accessToken, logout
├── ThemeContext (via ThemeProvider)
│   └── useTheme() → theme, toggleTheme
└── Local State (Hook state)
    ├── HomePage: useEntries(), useUser(), useEntryForm()
    ├── LoginPage: useAuthLogic()
    └── EntryCard: local state per audioSignedUrl
```

### Flusso Dati

**Caricamento Iniziale**:
```
App Mount
  ↓
AuthProvider: useAuthSessionManagement()
  ├─ GET Session da Supabase
  ├─ Set isLoading = true
  ├─ Subscribe onAuthStateChange
  └─ Set isLoading = false
  ↓
Router: Check isAuthenticated
  ├─ If false → /login
  └─ If true → HomePage
  ↓
HomePage Mount
  ├─ useUser() → GET /api/v1/user
  ├─ useEntries() → GET /api/v1/entries
  └─ useEntryForm() → ready per submit
```

**Creazione Nota**:
```
EntryForm.onSubmit
  ↓
useEntryForm.submitEntry()
  ├─ Validate content (not empty)
  ├─ If audioBlob:
  │   └─ supabaseStorage.uploadAudio()
  │       └─ POST /audio/{userId}/{entryId}.{ext}
  ├─ entriesService.addEntry()
  │   └─ POST /api/v1/entries + audio metadata
  ├─ HomePage recarica (window.location.reload)
  └─ useEntries recarica lista
```

---

## Autenticazione

### Flusso Passwordless (Magic Link)

1. **Login Request**
   ```
   Utente → LoginPage
         → Inserisce email
         → Click "Invia Link"
   ```

2. **OTP Send**
   ```
   useAuthLogic.sendMagicLink()
     → supabase.auth.signInWithOtp({
         email,
         shouldCreateUser: true
       })
     → Supabase invia email con link
   ```

3. **Email Confirmation**
   ```
   Utente riceve email
         → Clicca link (URL con token)
         → Browser si apre a `localhost:5173/?...code=...`
         → Supabase valida token
         → Crea/aggiorna sessione
   ```

4. **Session Update**
   ```
   useAuthSessionManagement.onAuthStateChange('SIGNED_IN')
     → Salva accessToken
     → Set isAuthenticated = true
   ```

5. **Redirect**
   ```
   Router rileva isAuthenticated = true
         → Reindirizza a HomePage
   ```

### Scadenza Session

- **Durata**: Token JWT ha scadenza (solitamente 1 ora)
- **Refresh**: Supabase refresh automatico all'accesso
- **Timeout**: Se session scade, 401 → Logout automatico → `/login`

### Logout

```
Header.handleLogout()
  ↓
useAuth.logout() → useAuthSessionManagement.logout()
  ↓
supabase.auth.signOut()
  ↓
onAuthStateChange('SIGNED_OUT')
  ↓
isAuthenticated = false
  ↓
Router reindirizza a /login
```

---

## Storage e Gestione File Audio

### `supabaseStorage.ts`
**Ruolo**: Operazioni storage file audio

**Metodi**:

#### `uploadAudio()`
```typescript
uploadAudio(
  blob: Blob,
  userId: string,
  entryId: string,
  mimeType: string
): Promise<{ audioPath: string; audioSizeBytes: number }>
```

**Flusso**:
1. Determina estensione da MIME type
2. Costruisce path: `audio/{userId}/{entryId}.{ext}`
3. Converte Blob a File
4. POST a Supabase Storage bucket `audio`
5. Ritorna path e size per metadata

#### `getSignedUrl()`
```typescript
getSignedUrl(audioPath: string): Promise<string>
```

**Utilizzo**:
- Genera URL temporario per accesso file audio
- Valido per 1 ora
- Usato da `EntryCard` per audio player

#### `deleteAudio()`
```typescript
deleteAudio(audioPath: string): Promise<void>
```

**Utilizzo**:
- Elimina file da storage
- Chiamato quando nota viene cancellata

### Bucket Storage

**Nome**: `audio`
**Struttura**:
```
audio/
├── {userId1}/
│   ├── {entryId1}.webm
│   ├── {entryId2}.mp4
│   └── ...
├── {userId2}/
│   └── ...
└── ...
```

**Sicurezza**: 
- Bucket privato (non public)
- Accesso solo via JWT token Supabase
- Signed URL per accesso frontend

---

## Tema e UI

### Tailwind CSS

**Configurazione**:
- Tema chiaro (default)
- Tema scuro (`.dark:` prefix)
- Breakpoints: `sm`, `md`, `lg`
- Colori custom da config

**Utilità Design**:
```tsx
// Responsive
<div className="w-full md:w-1/2 lg:w-1/3">

// Dark mode
<div className="bg-white dark:bg-gray-800 text-gray-800 dark:text-white">

// Tailwind components
<button className="px-4 py-2 bg-indigo-300 dark:bg-indigo-600 rounded-lg hover:opacity-80 transition">
```

### Theme Persistence

- Legge `localStorage.theme` al mount
- Aggiunge classe `.dark` a `<html>`
- Salva preferenza ad ogni toggle

### Colori Principali

| Elemento | Light | Dark |
|---|---|---|
| Background | gray-50 | gray-900 |
| Card | white | gray-800 |
| Header | indigo-300 | indigo-600 |
| Text | gray-800 | white |
| Border | gray-200 | gray-700 |

---

## Flusso dell'Applicazione

### Avvio App

```
1. main.tsx
   └─ createRoot().render()
      └─ ThemeProvider (load tema da localStorage)
         └─ BrowserRouter
            └─ AuthProvider
               └─ useAuthSessionManagement()
                  ├─ Check sessione Supabase
                  ├─ Subscribe onAuthStateChange
                  └─ Set isLoading = false
               └─ Router
                  ├─ Check isAuthenticated
                  ├─ Se true → HomePage
                  └─ Se false → LoginPage
```

### Autenticazione Utente

```
LoginPage
  ↓
useAuthLogic.sendMagicLink()
  └─ supabase.auth.signInWithOtp(email)
  └─ Email inviata
  ↓
LinkSentMessage (messaggio "controlla email")
  ↓
Utente clicca link email
  ↓
useAuthSessionManagement.onAuthStateChange('SIGNED_IN')
  └─ accessToken salvato
  ↓
Router reindirizza a HomePage
```

### Gestione Note (HomePage)

```
HomePage Mount
  ↓
useEntries.loadEntries()
  └─ GET /api/v1/entries
  ↓
useUser.fetchUser()
  └─ GET /api/v1/user
  ↓
Render:
  ├─ UserInfo (email utente)
  ├─ EntryForm (creazione nota)
  └─ EntryList
     └─ [EntryCard × N] (lista note)
```

### Creazione Nota

```
Utente digita testo in EntryForm
  ↓
[Opzionale] Registra audio via useAudioRecorder
  ├─ navigator.mediaDevices.getUserMedia()
  ├─ MediaRecorder raccoglie chunk
  ├─ UI mostra timer e progress bar
  └─ stopRecording() finalizza blob
  ↓
Utente clicca "Salva"
  ↓
useEntryForm.submitEntry()
  ├─ validate(): content not empty
  ├─ Se audioBlob: supabaseStorage.uploadAudio()
  │   └─ POST /audio/{userId}/{entryId}
  │   └─ Ritorna audioPath
  ├─ entriesService.addEntry()
  │   └─ POST /api/v1/entries (text + audio metadata)
  └─ HomePage.location.reload()
```

### Eliminazione Nota

```
Utente clicca bottone delete su EntryCard
  ↓
HomePage.handleDeleteEntry(id)
  ↓
useEntries.deleteEntry(id)
  ├─ entriesService.deleteEntry(id)
  │   └─ DELETE /api/v1/entries/{id}
  ├─ Backend elimina nota + audio
  └─ Frontend rimuove da lista
  ↓
UI aggiornata
```

### Logout

```
Utente clicca bottone Logout in Header
  ↓
Header.handleLogout()
  ├─ useAuth.logout()
  │   └─ supabase.auth.signOut()
  ├─ useAuthSessionManagement.onAuthStateChange('SIGNED_OUT')
  │   └─ isAuthenticated = false
  └─ navigate('/login')
  ↓
Router reindirizza a LoginPage
```

---

## Configurazione Ambiente

### File `.env.local`

```env
# Supabase
VITE_SUPABASE_URL=https://your-project.supabase.co
VITE_SUPABASE_ANON_KEY=your-anon-key

# API Backend
VITE_API_URL=http://localhost:5000
```

### Variabili Disponibili

| Variabile | Default | Uso |
|---|---|---|
| `VITE_SUPABASE_URL` | - | URL Supabase project |
| `VITE_SUPABASE_ANON_KEY` | - | Public anonymous key |
| `VITE_API_URL` | http://localhost:5000 | Base URL API backend |

---

## Build e Deploy

### Scripts

```bash
# Development
npm run dev              # Avvia Vite dev server

# Production
npm run build           # Build TypeScript + Vite
npm run preview         # Preview build locale

# Linting
npm run lint            # Esegui ESLint
```

### Build Output

- **Input**: `src/`
- **Output**: `dist/`
- **Include**: 
  - Bundle JavaScript (minified)
  - CSS compilato
  - Assets pubblici

---

## Best Practices

### 1. Componenti
- Componenti funzionali con hooks
- Props ben tipizzate con TypeScript
- JSDoc per componenti complessi

### 2. Hook
- Naming convention: `use*`
- Ritornare oggetti con proprietà chiare
- Cleanup effect dependencies

### 3. Autenticazione
- Sempre usare `useAuth()` per accesso token
- Non mescolare logica auth in componenti UI
- Logout automatico su 401

### 4. API
- Usare `apiClient` per richieste HTTP
- Tipizzare response con generics
- Gestire errori con field `error`

### 5. Audio
- Max 5 minuti registrazione
- Supportare multiple MIME types
- Cleanup stream e timer al unmount

### 6. Storage
- Signed URL valido 1 ora
- Path con userId per isolamento
- Eliminar file all'eliminazione nota

---

## Troubleshooting

### Token Non Disponibile
**Sintomo**: Errore "No authentication token found"
**Causa**: `useAuth().accessToken` è null
**Soluzione**: Verificare sessione Supabase attiva, logout e login

### Audio Non Funziona
**Sintomo**: Errore durante registrazione
**Causa**: Permesso microfono negato
**Soluzione**: Controllare impostazioni browser per localhost

### Note Non Caricate
**Sintomo**: HomePage vuota o errore API
**Causa**: Backend non raggiungibile o token scaduto
**Soluzione**: Verificare `VITE_API_URL`, controllare sessione

### Tema Non Persiste
**Sintomo**: Tema resets al refresh
**Causa**: localStorage disabilitato
**Soluzione**: Abilitare localStorage in browser

---

## File di Configurazione

### `vite.config.ts`
- Configurazione Vite e React plugin
- Alias e root directory

### `tailwind.config.js`
- Tema colori
- Configurazione utility CSS
- Dark mode class strategy

### `tsconfig.json`
- Opzioni TypeScript compiler
- Target e moduli ES
- Path aliases

### `eslint.config.js`
- Regole linting
- Parser TypeScript
- React hooks rules

---

## Conclusioni

Memoa FE è un'applicazione React moderna e ben strutturata che dimostra:

✅ Gestione autenticazione passwordless
✅ Architettura componenti pulita e modulare
✅ Uso avanzato di custom hooks
✅ Integrazione Supabase (auth + storage)
✅ Design responsivo e tema scuro
✅ Gestione file audio e streaming
✅ API REST tipizzata con TypeScript

L'applicazione è pronta per scalare e aggiungere nuove feature mantenendo la qualità del codice.
