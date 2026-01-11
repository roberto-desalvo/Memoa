# Architettura e Pattern - Memoa FE

## Indice

1. [Architettura Generale](#architettura-generale)
2. [Pattern di Stato](#pattern-di-stato)
3. [Pattern di Componenti](#pattern-di-componenti)
4. [Flusso Dati](#flusso-dati)
5. [Separazione delle Responsabilità](#separazione-delle-responsabilità)
6. [Error Handling](#error-handling)
7. [Performance Considerations](#performance-considerations)
8. [Testing Strategy](#testing-strategy)

---

## Architettura Generale

### Livelli Architetturali

```
┌─────────────────────────────────────────────┐
│           UI Components Layer               │
│  (React Components, Pages, Layouts)         │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│         Custom Hooks Layer                  │
│  (Business Logic, State Management)         │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│          Services Layer                     │
│  (API Calls, Storage Operations)            │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│       External Services Layer               │
│  (Supabase, Browser APIs)                   │
└─────────────────────────────────────────────┘
```

### Separation of Concerns

```
Components (UI)
  ↓
Hook (Business Logic)
  ↓
Service (API/Storage)
  ↓
External (Supabase, etc.)
```

**Vantaggi**:
- Components usano solo props e callback
- Logica business isolata in hooks
- Services gestiscono HTTP/Storage
- Facile testare ogni livello

---

## Pattern di Stato

### 1. Context + Hooks (Global State)

Usato per stato **globale** e **persistente**:

```typescript
// 1. Crea Context
const AuthContext = createContext<AuthContextType | undefined>(undefined);

// 2. Crea Provider
export const AuthProvider = ({ children }: Props) => {
  const { isAuthenticated, accessToken, logout } = useAuthSessionManagement();
  
  return (
    <AuthContext.Provider value={{ isAuthenticated, accessToken, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

// 3. Custom Hook per consumare
export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within AuthProvider');
  return context;
};
```

**Applicazioni in Memoa**:
- `AuthProvider` → Sessione utente, token
- `ThemeProvider` → Preferenza tema

**Quando usare**: Dati shared tra più componenti non imparentati

### 2. Custom Hooks (Local Logic)

Usato per logica **localizzata** e **riutilizzabile**:

```typescript
export const useEntryForm = (): UseEntryFormReturn => {
  const [content, setContent] = useState('');
  const [audioBlob, setAudioBlob] = useState<Blob | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  
  const submitEntry = useCallback(async () => {
    // Logica submit complessa
  }, [content, audioBlob]);
  
  return {
    content,
    setContent,
    audioBlob,
    setAudioBlob,
    submitEntry,
    // ...
  };
};
```

**Applicazioni in Memoa**:
- `useEntries` → Fetch e gestione lista note
- `useEntryForm` → Logica form creazione nota
- `useAudioRecorder` → Registrazione audio
- `useUser` → Dati utente corrente

**Quando usare**: Logica riutilizzabile o complessa

### 3. Component State (Local UI State)

Usato per stato **locale** e **temporaneo**:

```typescript
export const EntryCard = ({ entry, onDelete }: Props) => {
  const [audioSignedUrl, setAudioSignedUrl] = useState<string | null>(null);
  const [audioLoading, setAudioLoading] = useState(false);
  
  useEffect(() => {
    // Load signed URL when mounted
    if (entry.audioPath) {
      setAudioLoading(true);
      supabaseStorage.getSignedUrl(entry.audioPath)
        .then(setAudioSignedUrl)
        .finally(() => setAudioLoading(false));
    }
  }, [entry.audioPath]);
  
  return <div>...</div>;
};
```

**Quando usare**: UI state specifico componente (loading, expanded, etc.)

---

## Pattern di Componenti

### 1. Page Components

**Responsabilità**:
- Composizione layout pagina
- Orchestrazione hook e sottoinsiemi
- Gestione routing / props passate da router

**Esempio: HomePage**
```tsx
export const HomePage = () => {
  // Orchestrazione hook principali
  const { entries, deleteEntry } = useEntries();
  const { user } = useUser();
  const form = useEntryForm();
  
  // Handlers
  const handleAddEntry = useCallback(...) => {};
  const handleDeleteEntry = useCallback(...) => {};
  
  // Layout composition
  return (
    <main>
      <UserInfo user={user} />
      <EntryForm {...form} onSubmit={handleAddEntry} />
      <EntryList entries={entries} onDelete={handleDeleteEntry} />
    </main>
  );
};
```

**Caratteristiche**:
- Montate dal Router
- Top-level composition
- Iniettano callback in sottoinsiemi

### 2. Feature Components

**Responsabilità**:
- Logica di feature specifica
- Sub-composizione di componenti
- Gestione effetti side-effect

**Esempio: EntryForm**
```tsx
export const EntryForm = ({ 
  content, 
  onContentChange,
  onSubmit,
  ...
}: Props) => {
  const { isRecording, startRecording, stopRecording } = useAudioRecorder();
  
  return (
    <form onSubmit={onSubmit}>
      <textarea value={content} onChange={(e) => onContentChange(e.target.value)} />
      <button onClick={() => isRecording ? stopRecording() : startRecording()}>
        {isRecording ? 'Stop' : 'Record'}
      </button>
      <button type="submit">Save</button>
    </form>
  );
};
```

**Caratteristiche**:
- Ricevono callback da parent
- Usano hook per logica complessa
- Non mutano direttamente dati globali

### 3. Presentational Components

**Responsabilità**:
- Solo renderizzazione UI
- Ricevono dati via props
- Emettono callback per azioni

**Esempio: EntryCard**
```tsx
export const EntryCard = ({ entry, onDelete }: Props) => {
  const formatDate = (dateString: string) => { /* ... */ };
  
  return (
    <div className="card">
      <p>{entry.text}</p>
      <small>{formatDate(entry.createdAt)}</small>
      <button onClick={() => onDelete(entry.id)}>Delete</button>
    </div>
  );
};
```

**Caratteristiche**:
- Funzioni pure
- No hook (tranne useState per UI locale)
- Testa e mantieni facilmente

---

## Flusso Dati

### Unidirectional Data Flow

```
Parent (State)
  ↓
Props (down)
  ↓
Child (Render)
  ↓
User Interaction
  ↓
Callback (up)
  ↓
Parent (Update State)
```

**Esempio: HomePage → EntryForm → Create Entry**

```tsx
// HomePage (State Manager)
const HomePage = () => {
  const form = useEntryForm();
  
  const handleAddEntry = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      const success = await form.submitEntry();
      if (success) window.location.reload();
    },
    [form]
  );
  
  return (
    <EntryForm
      content={form.content}
      onContentChange={form.setContent}
      onSubmit={handleAddEntry}  // ← Callback
      {...form}
    />
  );
};

// EntryForm (Feature)
const EntryForm = ({ 
  content, 
  onContentChange, 
  onSubmit 
}: Props) => {
  return (
    <form onSubmit={onSubmit}>
      <textarea 
        value={content}
        onChange={(e) => onContentChange(e.target.value)}  // ← Update Parent
      />
      <button type="submit">Save</button>
    </form>
  );
};
```

### Data Flow con API

```
User Action
  ↓
Component Callback
  ↓
Hook (useEntryForm)
  ↓
Service (entriesService.addEntry)
  ↓
API Client (apiClient.post)
  ↓
HTTP Request
  ↓
Backend
  ↓
HTTP Response
  ↓
API Client (parse)
  ↓
Service (return)
  ↓
Hook (update state)
  ↓
Component (re-render)
  ↓
UI Updated
```

---

## Separazione delle Responsabilità

### Features per Feature

#### Feature: Authentication

**Componenti**:
- `LoginPage` → UI form login
- `LoginForm` → Form element
- `LinkSentMessage` → Messaggio conferma
- `AuthProvider` → Context stato auth
- `ProtectLogin`, `RequireAuth` → Guards (legacy)

**Hooks**:
- `useAuthLogic` → Logica form email+OTP
- `useAuthSessionManagement` → Gestione sessione
- `useAuth` → Accesso context
- `useIsAuthenticated` → Shortcut check

**Services**:
- Supabase Auth (signInWithOtp, signOut, onAuthStateChange)

**Responsabilità**:
```
UI (LoginPage)
  ↓
Logic (useAuthLogic)
  ↓
Session (useAuthSessionManagement)
  ↓
External (supabase.auth)
```

#### Feature: Entries (Note)

**Componenti**:
- `HomePage` → Orchestrazione principale
- `EntryForm` → Form creazione nota
- `EntryCard` → Visualizzazione nota singola
- `EntryList` → Lista note
- `UserInfo` → Dati utente
- `ErrorMessage` → Messaggi errore

**Hooks**:
- `useEntries` → Fetch e CRUD note
- `useEntryForm` → Logica form + upload audio
- `useAudioRecorder` → Registrazione audio
- `useUser` → Dati utente

**Services**:
- `entriesService` → API calls entries
- `supabaseStorage` → Upload/download audio

**Responsabilità**:
```
UI (HomePage, EntryForm, EntryCard)
  ↓
Business Logic (useEntries, useEntryForm, useAudioRecorder, useUser)
  ↓
API/Storage (entriesService, supabaseStorage)
  ↓
External (Backend API, Supabase Storage)
```

#### Feature: Theme

**Componenti**:
- `ThemeProvider` → Context tema

**Hooks**:
- `useTheme` → Accesso e toggle tema

**Storage**:
- `localStorage.theme` → Persistenza

**Responsabilità**:
```
UI (Header toggle button)
  ↓
Logic (useTheme.toggleTheme)
  ↓
Storage (localStorage + DOM class)
```

### Per Aspetto (Cross-Feature)

#### Autenticazione
- Centralizzato in `AuthProvider`
- Accesso via `useAuth()` ovunque
- Auto-redirect su 401

#### API Calls
- Centralizzato in `apiClient`
- Gestione token automatica
- Consistent error handling

#### Storage Audio
- Centralizzato in `supabaseStorage`
- Signed URLs per accesso file
- Cleanup on delete

#### UI Styling
- Tailwind CSS
- Theme via context
- Responsive via breakpoints

---

## Error Handling

### Strategia Multi-Layer

```
Layer 1: Network Error
  ↓ (fetch fail, no connectivity)
  ↓
Layer 2: API Error
  ↓ (4xx, 5xx responses)
  ↓
Layer 3: Business Logic Error
  ↓ (validation, state error)
  ↓
Layer 4: UI Feedback
  ↓ (toast, alert, message)
```

### Esempi

#### API Error Handling

```typescript
// Service
async addEntry(text: string): Promise<ApiResponse<Entry>> {
  try {
    const response = await apiClient.post('/api/v1/entries', { text });
    return response;
  } catch (error) {
    return { 
      error: error instanceof Error ? error.message : 'Unknown error',
      status: 0 
    };
  }
}

// Hook
async submitEntry(): Promise<boolean> {
  try {
    setSubmitError(null);
    const response = await entriesService.addEntry(content);
    if (response.success) {
      return true;
    } else {
      setSubmitError(response.error || 'Unknown error');
      return false;
    }
  } catch (err) {
    setSubmitError(err instanceof Error ? err.message : 'Unknown error');
    return false;
  }
}

// Component
const handleAddEntry = async (e: FormEvent) => {
  e.preventDefault();
  const success = await form.submitEntry();
  if (success) {
    // Mostra success feedback
  }
  // Altrimenti form.submitError è disponibile per mostrare messaggio
};
```

#### 401 Unauthorized Handling

```typescript
// apiClient
if (response.status === 401) {
  await this.handleUnauthorized();  // Logout + redirect
  return { error: 'Unauthorized', status: 401 };
}

// Automatico su ogni API call
```

#### Audio Error Handling

```typescript
// Hook
try {
  const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
} catch (err) {
  const message = err instanceof Error ? err.message : 'Errore accesso microfono';
  setError(message);
  setIsRecording(false);
}

// Component mostra error
{recordingError && <p className="text-red-500">{recordingError}</p>}
```

### Pattern: Error State

```typescript
// Hook fornisce error state
const { error } = useEntries();

// Component mostra errore
{error && <ErrorMessage message={error} />}
```

---

## Performance Considerations

### 1. Re-render Optimization

#### useCallback per Stabilità

```typescript
// ❌ Nuovo callback ogni render → child re-render inutile
const handleDelete = (id: string) => deleteEntry(id);

// ✅ Callback stabile → child non re-render
const handleDelete = useCallback(
  (id: string) => deleteEntry(id),
  [deleteEntry]
);
```

#### useMemo per Dati Complessi

```typescript
// Potenziale uso (non usato in Memoa attualmente)
const processedEntries = useMemo(
  () => entries.filter(...).sort(...),
  [entries]
);
```

### 2. Lazy Loading Audio

```typescript
// Audio Signed URL caricato al mount di EntryCard
useEffect(() => {
  if (entry.audioPath) {
    supabaseStorage.getSignedUrl(entry.audioPath)
      .then(setAudioSignedUrl);
  }
}, [entry.audioPath]);

// Non blocca render, loading state mostra agli utenti
```

### 3. Infinite Scroll (Potenziale)

```typescript
// EntryList potrebbe implementare infinite scroll
// Attualmente carica tutte le note, porebbe paginarle:
const [page, setPage] = useState(0);

useEffect(() => {
  const onScroll = () => {
    if (scrolledToBottom) {
      loadMoreEntries();
    }
  };
  
  window.addEventListener('scroll', onScroll);
  return () => window.removeEventListener('scroll', onScroll);
}, []);
```

### 4. Code Splitting (Vite)

```typescript
// Possibile con React.lazy()
const HomePage = React.lazy(() => import('./HomePage'));
const LoginPage = React.lazy(() => import('./LoginPage'));

// Wrappare con Suspense in Router
```

---

## Testing Strategy

### Testing per Layer

#### 1. Unit Tests (Hooks)

```typescript
// test: useAuthLogic.test.ts
describe('useAuthLogic', () => {
  it('should send magic link and show confirmation', async () => {
    const { result } = renderHook(() => useAuthLogic());
    
    await act(async () => {
      await result.current.sendMagicLink(fakeEvent);
    });
    
    expect(result.current.linkSent).toBe(true);
  });
});

// test: useEntryForm.test.ts
describe('useEntryForm', () => {
  it('should validate content is required', () => {
    const { result } = renderHook(() => useEntryForm());
    
    act(() => {
      result.current.setContent('');
    });
    
    const valid = result.current.validate();
    expect(valid).toBe(false);
    expect(result.current.validationError).toBeDefined();
  });
});
```

#### 2. Component Tests (React Testing Library)

```typescript
// test: EntryForm.test.tsx
describe('EntryForm', () => {
  it('should render form and handle submission', async () => {
    const onSubmit = jest.fn();
    render(<EntryForm onSubmit={onSubmit} {...props} />);
    
    const textarea = screen.getByRole('textbox');
    fireEvent.change(textarea, { target: { value: 'Test note' } });
    
    const button = screen.getByRole('button', { name: /save/i });
    fireEvent.click(button);
    
    expect(onSubmit).toHaveBeenCalled();
  });
});

// test: EntryCard.test.tsx
describe('EntryCard', () => {
  it('should display entry text and date', () => {
    const entry = { id: '1', text: 'Test', createdAt: '2026-01-11T00:00:00Z' };
    render(<EntryCard entry={entry} onDelete={jest.fn()} />);
    
    expect(screen.getByText('Test')).toBeInTheDocument();
  });
});
```

#### 3. Integration Tests

```typescript
// test: HomePage.integration.test.tsx
describe('HomePage Integration', () => {
  it('should load entries and allow creation', async () => {
    // Mock API responses
    server.use(
      rest.get('http://localhost:5000/api/v1/entries', (req, res, ctx) => {
        return res(ctx.json({ data: mockEntries }));
      })
    );
    
    render(<HomePage />);
    
    await waitFor(() => {
      expect(screen.getByText('Note 1')).toBeInTheDocument();
    });
    
    // Test creation flow
    const input = screen.getByPlaceholderText(/aggiungi nota/i);
    fireEvent.change(input, { target: { value: 'New Note' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    
    // Verify API call
    expect(mockAddEntry).toHaveBeenCalledWith('New Note');
  });
});
```

### Testing Utilities

```typescript
// test setup
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { server } from './mocks/server';

// MSW (Mock Service Worker) per mock API
beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

### What to Test

| Layer | Focus | Tools |
|---|---|---|
| Hooks | Business logic, state updates, callbacks | `@testing-library/react-hooks` |
| Components | Rendering, props, callbacks, state | `@testing-library/react` |
| Integration | Feature flows, API interaction | `@testing-library/react` + MSW |
| E2E | Complete user journeys | Cypress, Playwright |

---

## Best Practices Applicate

✅ **Separation of Concerns**
- Componenti per UI
- Hook per logica
- Service per API

✅ **Type Safety**
- TypeScript per errori compile-time
- Interface per contratti

✅ **Error Handling**
- Try-catch nei servizi
- Error state nei hook
- Feedback UI nei componenti

✅ **Responsive Design**
- Tailwind breakpoints
- Mobile-first approach

✅ **Accessibilità**
- Semantic HTML
- ARIA labels (in parte)
- Keyboard navigation (potenziale)

✅ **Performance**
- useCallback per stabilità
- Lazy loading audio
- Code splitting (potenziale)

---

## Miglioramenti Futuri

### 1. State Management
- Migrare da Context a **Zustand** per global state più robusto
- Ridurre boilerplate di Context + useContext

### 2. Data Fetching
- Aggiungere **React Query** per caching e sincronizzazione dati
- Gestire offline/online sync

### 3. Testing
- Setup completo con Jest e React Testing Library
- MSW per mock API
- GitHub Actions CI/CD

### 4. Performance
- Implementare infinite scroll in EntryList
- Code splitting lazy routes
- Image optimization (se aggiungere foto)

### 5. Accessibilità
- ARIA labels completi
- Keyboard navigation su form
- Focus management

### 6. Features
- Ricerca/filtro note
- Ordinamento date/creazione
- Share note con link
- Collaborazione real-time

---

## Conclusioni

L'architettura di Memoa FE dimostra:

🏗️ **Architettura Stratificata Pulita**
- Separazione UI → Logic → Services → External

🎯 **Pattern di Stato Appropriati**
- Context per global state
- Hooks per business logic
- Component state per UI

↔️ **Flusso Dati Unidirezionale**
- Props down
- Callback up

🛡️ **Type-Safe**
- TypeScript coverage
- Interface ben definite

⚠️ **Error Handling Robusto**
- Multi-layer error management
- User-friendly feedback

🚀 **Performance-Conscious**
- useCallback per stabilità
- Lazy loading
- Ottimizzazione re-render

Questa architettura scalerà bene con aggiunta di nuove feature e integrazione di state management più avanzato.
