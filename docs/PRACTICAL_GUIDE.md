# Guida Pratica e Sviluppo - Memoa FE

## Indice

1. [Setup e Avvio](#setup-e-avvio)
2. [Aggiungere Nuovi Componenti](#aggiungere-nuovi-componenti)
3. [Aggiungere Nuovi Hook](#aggiungere-nuovi-hook)
4. [Aggiungere Nuovi Endpoint API](#aggiungere-nuovi-endpoint-api)
5. [Modificare Componenti Esistenti](#modificare-componenti-esistenti)
6. [Debugging Guide](#debugging-guide)
7. [Checklist Sviluppo](#checklist-sviluppo)
8. [Troubleshooting Comune](#troubleshooting-comune)

---

## Setup e Avvio

### Prerequisiti

```bash
# Node.js e npm
node --version  # v18+ richiesto
npm --version   # v9+

# Git
git --version

# VS Code (raccomandato)
# Extensions consigliate:
# - ES7+ React/Redux/React-Native snippets
# - ESLint
# - Tailwind CSS IntelliSense
# - TypeScript Vue Plugin
```

### Primo Avvio

```bash
# 1. Clone repository
git clone https://github.com/roberto-desalvo/Memoa.git
cd Memoa/app

# 2. Installa dipendenze
npm install

# 3. Setup file ambiente
cp .env.example .env.local

# 4. Configura variabili ambiente
# Modifica .env.local con:
VITE_SUPABASE_URL=https://your-project.supabase.co
VITE_SUPABASE_ANON_KEY=your-anon-key
VITE_API_URL=http://localhost:5000

# 5. Avvia dev server
npm run dev

# 6. Apri browser
# Visita http://localhost:5173
```

### Comandi Disponibili

```bash
# Development
npm run dev                # Avvia Vite dev server (hot reload)
npm run build             # Build produzione (genera dist/)
npm run preview           # Preview build locale
npm run lint              # Esegui ESLint

# TypeScript
npm run type-check        # Verifica tipi TypeScript (se aggiunto)

# Utility
npm install {package}     # Installa pacchetto
npm uninstall {package}   # Rimuovi pacchetto
npm update                # Aggiorna dipendenze
```

### File Configurazione Importante

```
.env.local           # Variabili ambiente (GIT IGNORED)
vite.config.ts       # Configurazione build
tsconfig.json        # Configurazione TypeScript
tailwind.config.js   # Temi e colori Tailwind
eslint.config.js     # Regole linting
```

---

## Aggiungere Nuovi Componenti

### Template: Componente Presentazionale

**Percorso**: `src/features/{feature}/components/{Component}.tsx`

**Esempio**: Nuovo componente per mostrare statistiche

```tsx
import React from 'react';

interface StatsProps {
  totalEntries: number;
  totalAudio: number;
  lastModified: string;
}

/**
 * Mostra statistiche dell'utente
 * @component
 */
export const Stats: React.FC<StatsProps> = ({
  totalEntries,
  totalAudio,
  lastModified,
}) => {
  return (
    <div className="grid grid-cols-3 gap-4 p-6 bg-white dark:bg-gray-800 rounded-lg shadow">
      <div className="text-center">
        <p className="text-4xl font-bold text-indigo-600 dark:text-indigo-400">
          {totalEntries}
        </p>
        <p className="text-gray-600 dark:text-gray-400">Note</p>
      </div>
      
      <div className="text-center">
        <p className="text-4xl font-bold text-indigo-600 dark:text-indigo-400">
          {totalAudio}
        </p>
        <p className="text-gray-600 dark:text-gray-400">Con Audio</p>
      </div>
      
      <div className="text-center">
        <p className="text-sm text-gray-600 dark:text-gray-400">
          Ultima modifica
        </p>
        <p className="text-base font-semibold text-gray-800 dark:text-white">
          {new Date(lastModified).toLocaleDateString('it-IT')}
        </p>
      </div>
    </div>
  );
};
```

**Checklist**:
- ✅ TypeScript interfaces per props
- ✅ JSDoc comment
- ✅ Semantic HTML
- ✅ Tailwind classes per styling
- ✅ Dark mode support (`.dark:`)
- ✅ Responsive design

### Template: Componente con Logica

**Percorso**: `src/features/{feature}/{Component}.tsx`

**Esempio**: Pagina statistiche (pagina nuova)

```tsx
import { useMemo } from 'react';
import { useEntries } from '../../shared/hooks/useEntries';
import { Stats } from './components/Stats';
import { StatsLoading } from './components/StatsLoading';
import { ErrorMessage } from './components/ErrorMessage';

export const StatsPage = () => {
  const { entries, loading, error } = useEntries();

  // Calcola statistiche da entries
  const stats = useMemo(() => {
    return {
      totalEntries: entries.length,
      totalAudio: entries.filter(e => e.audioPath).length,
      lastModified: entries.length > 0 
        ? entries[0].createdAt 
        : new Date().toISOString(),
    };
  }, [entries]);

  if (loading) return <StatsLoading />;
  if (error) return <ErrorMessage message={error} />;

  return (
    <main className="max-w-4xl mx-auto px-4 py-6">
      <h1 className="text-3xl font-bold mb-8 text-gray-800 dark:text-white">
        Statistiche
      </h1>
      <Stats {...stats} />
    </main>
  );
};
```

**Steps**:
1. Crea file componente
2. Importa hook necessari
3. Gestisci loading/error
4. Definisci JSDoc
5. Esporta da `index.ts` (opzionale)

### Registrare Nuova Rotta

**Modifica**: `src/app/router.tsx`

```tsx
import { StatsPage } from '../features/stats/StatsPage';

export const Router = () => {
  const { isAuthenticated } = useAuth();

  return (
    <Routes>
      <Route element={<RootLayout />}>
        {isAuthenticated ? (
          <>
            <Route path="/" element={<HomePage />} />
            <Route path="/stats" element={<StatsPage />} />  {/* ← Nuova rotta */}
            <Route path="*" element={<Navigate to="/" replace />} />
          </>
        ) : (
          // ...
        )}
      </Route>
    </Routes>
  );
};
```

**Aggiungere Link nel Header**:

```tsx
// src/shared/Header.tsx
<nav className="flex gap-4">
  <button onClick={() => navigate('/')}>Home</button>
  <button onClick={() => navigate('/stats')}>Stats</button>  {/* ← Link */}
  {/* ... */}
</nav>
```

---

## Aggiungere Nuovi Hook

### Template: Hook Semplice

**Percorso**: `src/shared/hooks/use{Name}.ts`

**Esempio**: Hook per contare note

```typescript
import { useMemo } from 'react';
import type { Entry } from '../api/types';

export interface UseEntryCountReturn {
  totalEntries: number;
  audioEntries: number;
  percentage: number;
}

/**
 * Calcola statistiche conteggio note
 * @param entries - Array di note
 * @returns Statistiche conteggio
 */
export const useEntryCount = (entries: Entry[]): UseEntryCountReturn => {
  const stats = useMemo(() => {
    const audioCount = entries.filter(e => e.audioPath).length;
    const percentage = entries.length > 0
      ? Math.round((audioCount / entries.length) * 100)
      : 0;

    return {
      totalEntries: entries.length,
      audioEntries: audioCount,
      percentage,
    };
  }, [entries]);

  return stats;
};
```

**Utilizzo**:
```tsx
const HomePage = () => {
  const { entries } = useEntries();
  const { totalEntries, audioEntries } = useEntryCount(entries);
  
  return <div>{totalEntries} note, {audioEntries} con audio</div>;
};
```

### Template: Hook Complesso

**Percorso**: `src/shared/hooks/use{Name}.ts`

**Esempio**: Hook per filtrare e ordinare note

```typescript
import { useState, useMemo, useCallback } from 'react';
import type { Entry } from '../api/types';

export type SortBy = 'newest' | 'oldest' | 'name';
export type FilterBy = 'all' | 'audio' | 'text';

export interface UseEntriesFilterReturn {
  filtered: Entry[];
  sortBy: SortBy;
  filterBy: FilterBy;
  setSortBy: (sort: SortBy) => void;
  setFilterBy: (filter: FilterBy) => void;
  reset: () => void;
}

/**
 * Filter e sort entries
 * @param entries - Lista note originale
 * @returns Filtered entries + controls
 */
export const useEntriesFilter = (entries: Entry[]): UseEntriesFilterReturn => {
  const [sortBy, setSortBy] = useState<SortBy>('newest');
  const [filterBy, setFilterBy] = useState<FilterBy>('all');

  const filtered = useMemo(() => {
    // Filter
    let result = entries;
    switch (filterBy) {
      case 'audio':
        result = result.filter(e => e.audioPath);
        break;
      case 'text':
        result = result.filter(e => !e.audioPath);
        break;
      case 'all':
      default:
        break;
    }

    // Sort
    switch (sortBy) {
      case 'newest':
        result = [...result].sort((a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        break;
      case 'oldest':
        result = [...result].sort((a, b) =>
          new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
        );
        break;
      case 'name':
        result = [...result].sort((a, b) =>
          a.text.localeCompare(b.text)
        );
        break;
    }

    return result;
  }, [entries, sortBy, filterBy]);

  const reset = useCallback(() => {
    setSortBy('newest');
    setFilterBy('all');
  }, []);

  return {
    filtered,
    sortBy,
    filterBy,
    setSortBy,
    setFilterBy,
    reset,
  };
};
```

**Utilizzo**:
```tsx
const HomePage = () => {
  const { entries } = useEntries();
  const { filtered, sortBy, setSortBy, filterBy, setFilterBy } = 
    useEntriesFilter(entries);
  
  return (
    <div>
      <select value={sortBy} onChange={(e) => setSortBy(e.target.value as SortBy)}>
        <option value="newest">Più recenti</option>
        <option value="oldest">Più vecchi</option>
        <option value="name">Nome</option>
      </select>
      
      <div>{filtered.map(entry => <EntryCard key={entry.id} entry={entry} />)}</div>
    </div>
  );
};
```

**Checklist Hook**:
- ✅ Export interface di return
- ✅ JSDoc con param e return
- ✅ useMemo per expensive calculations
- ✅ useCallback per callbacks stabili
- ✅ Cleanup effects se necessario
- ✅ Type-safe con TypeScript

---

## Aggiungere Nuovi Endpoint API

### Scenario: Aggiungere Endpoint di Ricerca

**Step 1: Definire Tipi**

```typescript
// src/shared/api/types.ts
export type SearchEntryRequest = {
  query: string;
  limit?: number;
};

export type SearchEntryResponse = {
  results: Entry[];
  totalFound: number;
};
```

**Step 2: Aggiungere al Service**

```typescript
// src/shared/api/entriesService.ts
/**
 * Search entries by text
 * GET /api/v1/entries/search?query={query}&limit={limit}
 */
async searchEntries(
  query: string,
  limit: number = 20
): Promise<ApiResponse<SearchEntryResponse>> {
  const params = new URLSearchParams({
    query,
    limit: limit.toString(),
  });
  
  return apiClient.get<SearchEntryResponse>(
    `/api/v1/entries/search?${params.toString()}`
  );
}
```

**Step 3: Creare Hook**

```typescript
// src/shared/hooks/useEntrySearch.ts
import { useState, useCallback } from 'react';
import { entriesService } from '../api/entriesService';
import type { Entry } from '../api/types';

export const useEntrySearch = () => {
  const [results, setResults] = useState<Entry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const search = useCallback(async (query: string) => {
    if (!query.trim()) {
      setResults([]);
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await entriesService.searchEntries(query);
      if (response.data) {
        setResults(response.data.results);
      } else {
        setError(response.error || 'Errore ricerca');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Errore');
    } finally {
      setLoading(false);
    }
  }, []);

  const clear = useCallback(() => {
    setResults([]);
    setError(null);
  }, []);

  return {
    results,
    loading,
    error,
    search,
    clear,
  };
};
```

**Step 4: Usare in Componente**

```typescript
// src/features/entries/components/SearchBar.tsx
export const SearchBar = () => {
  const { results, loading, search, clear } = useEntrySearch();
  const [query, setQuery] = useState('');

  const handleSearch = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const value = e.target.value;
      setQuery(value);
      search(value);
    },
    [search]
  );

  return (
    <div>
      <input
        type="text"
        placeholder="Cerca note..."
        value={query}
        onChange={handleSearch}
        className="w-full px-4 py-2 rounded-lg border border-gray-300 dark:border-gray-600"
      />
      
      {loading && <p>Ricerca in corso...</p>}
      
      <div>
        {results.map(entry => (
          <div key={entry.id}>{entry.text}</div>
        ))}
      </div>
    </div>
  );
};
```

**Checklist Nuovo Endpoint**:
- ✅ Definire types request/response
- ✅ Aggiungere metodo al service
- ✅ Creare hook wrapper
- ✅ Gestire loading/error
- ✅ Creare componente UI
- ✅ Integrare in layout
- ✅ Testare con DevTools Network

---

## Modificare Componenti Esistenti

### Modifica 1: Aggiungere Campo a Form

**Target**: `src/features/entries/components/EntryForm.tsx`

**Passo**: Aggiungere campo "categoria" nota

```tsx
// Aggiungere prop
interface EntryFormProps {
  content: string;
  category: string;  // ← Nuovo
  onContentChange: (value: string) => void;
  onCategoryChange: (value: string) => void;  // ← Nuovo
  // ... rest
}

// Nel componente
export const EntryForm = ({
  content,
  category,  // ← Ricevi
  onContentChange,
  onCategoryChange,  // ← Ricevi
  // ...
}: EntryFormProps) => {
  return (
    <form>
      <textarea
        value={content}
        onChange={(e) => onContentChange(e.target.value)}
      />
      
      {/* Nuovo campo */}
      <select
        value={category}
        onChange={(e) => onCategoryChange(e.target.value)}
        className="w-full px-4 py-2 rounded-lg border"
      >
        <option value="">Seleziona categoria</option>
        <option value="work">Lavoro</option>
        <option value="personal">Personale</option>
        <option value="ideas">Idee</option>
      </select>
      
      <button type="submit">Salva</button>
    </form>
  );
};
```

**Aggiornare Parent** (HomePage.tsx):

```tsx
const HomePage = () => {
  const form = useEntryForm();
  const [category, setCategory] = useState('');  // ← Nuovo state

  return (
    <EntryForm
      content={form.content}
      category={category}  // ← Passa
      onContentChange={form.setContent}
      onCategoryChange={setCategory}  // ← Passa
      // ...
    />
  );
};
```

### Modifica 2: Aggiungere Validazione

**Target**: `src/shared/hooks/useEntryForm.ts`

**Passo**: Validare lunghezza minima testo

```typescript
const validate = useCallback((): boolean => {
  const trimmedContent = content.trim();
  
  // Vuoto
  if (!trimmedContent) {
    setValidationError('Il contenuto della nota è obbligatorio');
    return false;
  }
  
  // Troppo corto
  if (trimmedContent.length < 5) {
    setValidationError('La nota deve contenere almeno 5 caratteri');
    return false;
  }
  
  // Troppo lungo
  if (trimmedContent.length > 1000) {
    setValidationError('La nota non può superare 1000 caratteri');
    return false;
  }

  return true;
}, [content]);
```

### Modifica 3: Aggiungere Funzionalità Stile

**Target**: `src/features/entries/components/EntryCard.tsx`

**Passo**: Mostrare badge per note con audio

```tsx
export const EntryCard = ({ entry, onDelete }: Props) => {
  return (
    <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-4">
      <div className="flex justify-between items-start mb-3">
        <p className="flex-1 text-gray-600">{entry.text}</p>
        
        {/* Badge audio */}
        {entry.audioPath && (
          <span className="ml-2 px-2 py-1 bg-blue-100 dark:bg-blue-900 text-blue-700 dark:text-blue-200 text-xs rounded-full">
            🎙️ Audio
          </span>
        )}
      </div>
      
      {/* Rest */}
    </div>
  );
};
```

---

## Debugging Guide

### Scenario 1: Componente Non Renderizza

**Checklist**:

```tsx
// 1. Verifica export
export const MyComponent = () => { };
// ✅ Named export o default export

// 2. Verifica import
import { MyComponent } from './path';
// ✅ Path corretto, estensione .tsx

// 3. Verifica prop types
interface Props {
  title: string;  // ← Se string, passa string!
}
// ✅ Props corrisponde a utilizzo

// 4. Verifica JSX syntax
return <div>{value}</div>;
// ✅ Parentesi chiuse, valid JSX

// 5. Check console
// Apri DevTools → Console
// ✅ No errors
```

### Scenario 2: API Call Non Funziona

```typescript
// 1. Verifica token disponibile
const { isAuthenticated, accessToken } = useAuth();
console.log('Auth:', { isAuthenticated, token: accessToken });
// ✅ accessToken è valorizzato

// 2. Verifica endpoint URL
console.log('API URL:', import.meta.env.VITE_API_URL);
// ✅ Corretto

// 3. Verifica headers
// DevTools → Network → Click richiesta → Headers
// ✅ Authorization: Bearer {token}

// 4. Verifica response
// DevTools → Network → Click richiesta → Response
// ✅ JSON valido

// 5. Verifica error handling
const response = await apiClient.get('/api/v1/entries');
if (response.data) {
  console.log('Success:', response.data);
} else {
  console.log('Error:', response.error);
}
```

### Scenario 3: Audio Non Funziona

```typescript
// 1. Verifica permesso microfono
try {
  const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
  console.log('Permesso microfono: OK');
} catch (err) {
  console.error('Permesso microfono: DENIED', err);
}
// ✅ Permesso accordato

// 2. Verifica Supabase Storage
// DevTools → Application → Storage
// ✅ Bucket 'audio' esiste

// 3. Verifica file upload
// DevTools → Network → Cercare upload richiesta
// ✅ Status 200 OK

// 4. Verifica signed URL
const url = await supabaseStorage.getSignedUrl('path/to/file');
console.log('URL:', url);
// ✅ URL valido, testabile in browser
```

### Scenario 4: State Non Aggiorna

```typescript
// ❌ Non aggiorna
const [count, setCount] = useState(0);
count = count + 1;  // Mutazione diretta!

// ✅ Aggiorna
setCount(count + 1);

// ❌ Non aggiorna dependency
useEffect(() => {
  setEntries(newEntries);
}, []);  // Se newEntries cambia, non esegue!

// ✅ Aggiorna
useEffect(() => {
  setEntries(newEntries);
}, [newEntries]);  // Includi nel dependency array
```

### React DevTools

**Install Extension**:
```
Chrome: React Developer Tools
Firefox: React Developer Tools
```

**Utilizzo**:
1. Apri DevTools → Components tab
2. Ispeziona componente
3. Vedi props e state
4. Edit state per testare

---

## Checklist Sviluppo

### Prima di Committare

- [ ] Codice compila senza errori (`npm run build`)
- [ ] Nessun errore console in dev
- [ ] Nessun warning ESLint (`npm run lint`)
- [ ] Componente risponde a tutte le azioni
- [ ] Tema scuro funziona (non breakspaces)
- [ ] Responsivo su mobile (f12 device mode)
- [ ] API calls vanno a buon fine (DevTools Network)
- [ ] Error handling funziona
- [ ] Loading states visibili
- [ ] Accessibility base (aria-labels, semantics)

### Commit Message

```bash
# Formato: {type}: {description}
git commit -m "feat: add search functionality for entries"
git commit -m "fix: resolve audio playback on mobile"
git commit -m "refactor: extract AudioPlayer component"
git commit -m "docs: update API documentation"

# Types: feat, fix, refactor, docs, style, test, chore
```

### Pull Request

- [ ] Descrizione chiara del cambiamento
- [ ] Riferimento a issue (se esiste)
- [ ] Screenshots/GIFs per UI changes
- [ ] Checklist auto-review completata
- [ ] No conflitti con main

---

## Troubleshooting Comune

### "Module not found" Error

```
❌ Cannot find module './EntryForm'
  at Resolver.resolveAbsolutePath...
```

**Soluzione**:
```typescript
// ❌ Sbagliato
import { EntryForm } from './EntryForm';

// ✅ Corretto
import { EntryForm } from './EntryForm.tsx';
// o usa path completo relativo
import { EntryForm } from '../../features/entries/components/EntryForm';
```

### "No authentication token found"

```
❌ Error: No authentication token found
```

**Soluzione**:
```typescript
// Verifica token disponibile
const { accessToken } = useAuth();
if (!accessToken) {
  console.log('Token non disponibile, effettua login');
  return;
}

// Verifica sessione Supabase
const { data: { session } } = await supabase.auth.getSession();
if (!session) {
  console.log('Sessione scaduta');
  // Logout e reindirizza a login
}
```

### "Type 'string' is not assignable to type 'Entry'"

```
❌ Type 'string' is not assignable to type 'Entry'
  Type 'string' is missing the following properties:
  id, text, createdAt
```

**Soluzione**:
```typescript
// ❌ Sbagliato
const entry: Entry = "my note";

// ✅ Corretto
const entry: Entry = {
  id: '123',
  text: 'my note',
  createdAt: new Date().toISOString(),
};
```

### "Cannot read property 'audioPath' of undefined"

```
❌ Cannot read property 'audioPath' of undefined
```

**Soluzione**:
```typescript
// ❌ Non verifica existence
<audio src={entry.audioPath}>

// ✅ Verifica prima
{entry.audioPath && <audio src={entry.audioPath}>}

// o usa optional chaining
<audio src={entry?.audioPath}>
```

### "fetch failed, TypeError: Failed to fetch"

```
❌ TypeError: Failed to fetch
```

**Soluzione**:
```typescript
// Verifica API URL
console.log('API URL:', import.meta.env.VITE_API_URL);

// Verifica backend in esecuzione
// curl http://localhost:5000/health

// Verifica CORS (se backend su porta diversa)
// Backend deve avere Access-Control-Allow-Origin header

// Verifica firewall
// Porta 5000 non bloccata
```

### "Styled component not applying"

```
❌ Tailwind classes non applicate
```

**Soluzione**:
```tsx
// ❌ Dynamic classes non funzionano
const color = 'red';
<div className={`text-${color}-500`}>  // ❌ Not applied!

// ✅ Usa classnames o switch
import classNames from 'classnames';
<div className={classNames('text-500', {
  'text-red-500': color === 'red',
  'text-blue-500': color === 'blue',
})}>

// ✅ o applica inline style
<div style={{ color: color }}>
```

### "Permission denied" per audio

```
❌ NotAllowedError: Permission denied
```

**Soluzione**:
```typescript
// Verifica permesso browser
// Browser Settings → Privacy → Microphone → Allow

// Verifica https (microphone richiede secure context)
// localhost:5173 → OK
// 192.168.x.x:5173 → NO (a meno che non localhost)

// Gestisci errore UI
catch (err) {
  if (err.name === 'NotAllowedError') {
    console.log('Permesso microfono negato');
    // Mostra messaggio utente
  }
}
```

---

## Deployment

### Build per Produzione

```bash
# Build
npm run build

# Preview (test build locale)
npm run preview

# Output: dist/ folder
# Contiene HTML, JS, CSS minified e ottimizzato
```

### Deploy a Vercel

```bash
# 1. Push a GitHub
git push origin main

# 2. Connetti repo a Vercel
# vercel.com → Import Project

# 3. Configura env vars
# Environment Variables:
# VITE_SUPABASE_URL=...
# VITE_SUPABASE_ANON_KEY=...
# VITE_API_URL=https://api.example.com

# 4. Deploy
# Automatic on push to main
```

### Deploy a Netlify

```bash
# 1. Connetti GitHub
# netlify.com → New site from Git

# 2. Configura
# Build command: npm run build
# Publish directory: dist

# 3. Env vars
# Build & deploy → Environment
```

---

## Conclusioni

Questa guida copre:

✅ Setup iniziale
✅ Aggiungere nuovi componenti
✅ Aggiungere nuovi hook
✅ Aggiungere nuovi endpoint
✅ Modificare componenti
✅ Debugging strategie
✅ Troubleshooting comune
✅ Deploy

Segui questa guida per sviluppare nuove feature mantenendo qualità e consistenza del codice!
