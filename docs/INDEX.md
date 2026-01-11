# Documentazione Frontend Memoa - Indice Principale

Benvenuto nella documentazione completa del frontend Memoa! Questa documentazione copre l'architettura, l'implementazione, e le guide pratiche per lo sviluppo.

## 📚 Documenti Disponibili

### 1. [FRONTEND_DOCUMENTATION.md](./FRONTEND_DOCUMENTATION.md)
**La guida completa e definitiva del progetto frontend**

Contenuto:
- Panoramica del progetto e caratteristiche
- Stack tecnologico e dipendenze
- Struttura del progetto dettagliata
- Descrizione completa di tutti i componenti React
- Descrizione completa di tutti gli hook personalizzati
- Servizi API e loro utilizzo
- Gestione dello stato (Context + Hooks)
- Sistema di autenticazione passwordless
- Storage e gestione file audio
- Tema e UI con Tailwind CSS
- Flusso completo dell'applicazione

**Quando leggere**: 
- Primo approccio con il progetto
- Comprendere come funziona ogni componente/hook
- Capire l'architettura generale

**Tempo di lettura**: ~45 minuti

---

### 2. [ARCHITECTURE_PATTERNS.md](./ARCHITECTURE_PATTERNS.md)
**Approfondimento su architettura e pattern di design**

Contenuto:
- Architettura generale (layer stack)
- Pattern di gestione dello stato
- Pattern di componenti (Page, Feature, Presentational)
- Flusso dati unidirezionale
- Separazione delle responsabilità per feature
- Error handling multi-layer
- Performance considerations
- Testing strategy

**Quando leggere**:
- Comprendere come è organizzato il codice
- Imparare i pattern usati nel progetto
- Pianificare nuove feature seguendo gli stessi pattern
- Optimizzare performance

**Tempo di lettura**: ~30 minuti

---

### 3. [API_FLOWS.md](./API_FLOWS.md)
**Dettagli su integrazione API e flussi di comunicazione**

Contenuto:
- Architettura API (layer stack)
- Client HTTP generico (apiClient.ts)
- Service specifico per entries (entriesService.ts)
- Endpoint disponibili (GET/POST/PUT/DELETE)
- Flussi dettagliati per ogni operazione
- Autenticazione API (Bearer tokens)
- Error handling API
- Integrazione Supabase (Auth + Storage)
- Debugging e logging

**Quando leggere**:
- Comprendere come funzionano le API calls
- Aggiungere nuovi endpoint
- Debuggare problemi di API
- Capire autenticazione e tokens

**Tempo di lettura**: ~40 minuti

---

### 4. [PRACTICAL_GUIDE.md](./PRACTICAL_GUIDE.md)
**Guida pratica per lo sviluppo e troubleshooting**

Contenuto:
- Setup iniziale e avvio progetto
- Template e step per aggiungere nuovi componenti
- Template e step per aggiungere nuovi hook
- Template e step per aggiungere nuovi endpoint
- Guide per modificare componenti esistenti
- Debugging guide per scenari comuni
- Checklist di sviluppo
- Troubleshooting errori comuni
- Deploy a Vercel/Netlify

**Quando leggere**:
- Prima di iniziare a sviluppare
- Quando devi aggiungere una nuova feature
- Quando incontri errori
- Prima di committare codice

**Tempo di lettura**: ~35 minuti

---

## 🎯 Percorsi di Lettura Consigliati

### Per Principianti (Nuovo nel Progetto)

1. Leggi **FRONTEND_DOCUMENTATION.md** (panoramica e concetti base)
2. Leggi **ARCHITECTURE_PATTERNS.md** (come è organizzato il codice)
3. Consulta **PRACTICAL_GUIDE.md** quando inizi a sviluppare

**Tempo totale**: ~2 ore

---

### Per Sviluppatori Intermedi

1. Ripassi velocemente **FRONTEND_DOCUMENTATION.md** (specifiche componenti che usi)
2. Consulta **ARCHITECTURE_PATTERNS.md** per pattern specifici
3. Usa **API_FLOWS.md** quando lavori con API calls
4. Usa **PRACTICAL_GUIDE.md** per aggiungere feature

**Tempo totale**: ~1.5 ore (+ tempo sviluppo)

---

### Per Contribuitori (Feature Nuove)

1. Consulta **PRACTICAL_GUIDE.md** per template e step
2. Riferisciti a **ARCHITECTURE_PATTERNS.md** per mantenere consistenza
3. Usa **API_FLOWS.md** se la feature richiede API call
4. Usa **FRONTEND_DOCUMENTATION.md** come reference per specifici file

**Tempo totale**: Variabile (+ tempo sviluppo)

---

### Per Debugging

1. Consulta **PRACTICAL_GUIDE.md** - Debugging Guide section
2. Usa **API_FLOWS.md** - Debugging e Logging section per API issues
3. Consulta **FRONTEND_DOCUMENTATION.md** per dettagli specifici file/componente

**Tempo totale**: 15-30 minuti

---

## 📋 Quick Reference

### Componenti Principali

| Componente | File | Descrizione |
|---|---|---|
| **AuthProvider** | `features/auth/AuthProvider.tsx` | Context autenticazione globale |
| **LoginPage** | `features/auth/LoginPage.tsx` | Pagina login con magic link |
| **HomePage** | `features/entries/HomePage.tsx` | Hub principale gestione note |
| **EntryForm** | `features/entries/components/EntryForm.tsx` | Form creazione nota con audio |
| **EntryCard** | `features/entries/components/EntryCard.tsx` | Card singola nota |
| **Header** | `shared/Header.tsx` | Header navigazione |
| **ThemeProvider** | `shared/ThemeProvider.tsx` | Context tema scuro/chiaro |
| **Router** | `app/router.tsx` | Configurazione routing |

### Hook Personalizzati

| Hook | File | Descrizione |
|---|---|---|
| **useAuth** | `features/auth/AuthProvider.tsx` | Accesso context autenticazione |
| **useAuthLogic** | `shared/hooks/useAuthLogic.ts` | Logica form login (email + OTP) |
| **useAuthSessionManagement** | `shared/hooks/useAuthSessionManagement.ts` | Gestione sessione globale |
| **useEntries** | `shared/hooks/useEntries.ts` | Fetch e CRUD note |
| **useEntryForm** | `shared/hooks/useEntryForm.ts` | Logica form creazione nota |
| **useAudioRecorder** | `shared/hooks/useAudioRecorder.ts` | Registrazione audio da microfono |
| **useUser** | `shared/hooks/useUser.ts` | Dati utente corrente |
| **useTheme** | `shared/ThemeProvider.tsx` | Accesso context tema |

### Servizi API

| Servizio | File | Descrizione |
|---|---|---|
| **apiClient** | `shared/lib/apiClient.ts` | Client HTTP generico con auth |
| **entriesService** | `shared/api/entriesService.ts` | Operazioni note (GET, POST, DELETE) |
| **supabaseStorage** | `shared/lib/supabaseStorage.ts` | Upload e accesso file audio |
| **supabase** | `shared/lib/supabaseClient.ts` | Inizializzazione Supabase |

### Configurazione

| File | Descrizione |
|---|---|
| `.env.local` | Variabili ambiente (API URL, Supabase keys) |
| `vite.config.ts` | Configurazione build Vite |
| `tailwind.config.js` | Tema colori e configurazione styling |
| `tsconfig.json` | Configurazione TypeScript |
| `eslint.config.js` | Regole linting |
| `package.json` | Dipendenze e script |

---

## 🔍 Come Trovare Cose

### Cerco informazioni su...

**...un componente React specifico**
→ FRONTEND_DOCUMENTATION.md → Sezione "Componenti React"

**...un hook personalizzato**
→ FRONTEND_DOCUMENTATION.md → Sezione "Hook Personalizzati"

**...come aggiungere una feature**
→ PRACTICAL_GUIDE.md → Sezioni appropriate (Componente/Hook/Endpoint)

**...come fanno le API calls**
→ API_FLOWS.md → Sezione "Flussi Dettagliati"

**...error handling**
→ ARCHITECTURE_PATTERNS.md → Sezione "Error Handling"

**...test**
→ ARCHITECTURE_PATTERNS.md → Sezione "Testing Strategy"

**...performance**
→ ARCHITECTURE_PATTERNS.md → Sezione "Performance Considerations"

**...debugging**
→ PRACTICAL_GUIDE.md → Sezione "Debugging Guide"

**...errori comuni**
→ PRACTICAL_GUIDE.md → Sezione "Troubleshooting Comune"

---

## 🚀 Comandi Utili

### Avvio Sviluppo

```bash
npm install              # Installa dipendenze
npm run dev             # Avvia dev server (hot reload)
npm run build           # Build produzione
npm run preview         # Anteprima build locale
npm run lint            # Esegui ESLint
```

### Debugging

```bash
# Apri browser DevTools (F12)
# Network tab: Vedi API calls
# Console tab: Leggi log e errori
# Application tab: Vedi localStorage, cookies

# React DevTools extension
# Ispeziona componenti e state

# VS Code Debugger
# Breakpoint su linee di codice
# F5 per debug con Chrome
```

---

## 📊 Struttura Informazioni Documentazione

```
Documentazione
│
├─ FRONTEND_DOCUMENTATION.md (Visione d'insieme)
│  ├─ Cosa è il progetto
│  ├─ Stack tecnologico
│  ├─ Descrizione ogni file/componente/hook
│  ├─ Come funziona l'applicazione
│  └─ Flussi base
│
├─ ARCHITECTURE_PATTERNS.md (Come è progettato)
│  ├─ Layer architetturali
│  ├─ Pattern di stato
│  ├─ Pattern di componenti
│  ├─ Separazione responsabilità
│  ├─ Error handling
│  ├─ Performance
│  └─ Testing
│
├─ API_FLOWS.md (Dettagli tecnici API)
│  ├─ Architettura API
│  ├─ Client HTTP e Service
│  ├─ Endpoint disponibili
│  ├─ Flussi operazioni (fetch, create, delete, etc)
│  ├─ Autenticazione
│  ├─ Supabase integration
│  └─ Debugging API
│
└─ PRACTICAL_GUIDE.md (Come sviluppare)
   ├─ Setup iniziale
   ├─ Aggiungere componenti (template)
   ├─ Aggiungere hook (template)
   ├─ Aggiungere API (template)
   ├─ Modificare componenti
   ├─ Debugging guide
   ├─ Checklist sviluppo
   ├─ Troubleshooting
   └─ Deploy
```

---

## 🎓 Concetti Chiave

### Flusso Dati
```
User Action → Component → Hook → Service → API → Backend → Response → Hook Update → Component Re-render → UI
```

### Stack Tecnologico
- **Frontend**: React 19 + TypeScript + Vite
- **Styling**: Tailwind CSS
- **Routing**: React Router v7
- **Auth**: Supabase Magic Link (passwordless)
- **Storage**: Supabase Storage (audio files)
- **API Communication**: Fetch API + custom client

### Patternarcheticolo
- **Stato Globale**: Context + Custom Hooks
- **Componenti**: Presentational + Feature + Page
- **Servizi**: API Client + Domain Services
- **Error Handling**: Multi-layer (Network → API → Business → UI)

---

## 💡 Tips

1. **Usa TypeScript** - Sfrutta type-safety, leggi gli errori dal compilatore
2. **Leggi console** - Controlla DevTools Console per errori e log
3. **Network tab** - Osserva API calls per capire flow
4. **Commit spesso** - Piccoli commit atomici sono meglio di grandi
5. **Test localmente** - Prima di pushare, testa su dev server
6. **Dark mode** - Verifica sempre con tema scuro abilitato
7. **Mobile** - Testa su mobile view (F12 → Device Mode)
8. **Documentazione** - Leggi JSDoc nei file, aiuta a capire

---

## 📞 Supporto e Domande

Se hai domande:

1. **Sulla struttura**: Leggi ARCHITECTURE_PATTERNS.md
2. **Su un file specifico**: Leggi FRONTEND_DOCUMENTATION.md
3. **Su una feature**: Segui template in PRACTICAL_GUIDE.md
4. **Su un errore**: Consulta PRACTICAL_GUIDE.md - Troubleshooting
5. **Su API**: Leggi API_FLOWS.md

---

## 📅 Storico Versioni Documentazione

| Versione | Data | Note |
|---|---|---|
| 1.0 | 11 Gennaio 2026 | Documentazione iniziale completa |

---

## 🎯 Next Steps

1. **Scegli percorso** di lettura suggerito (Principiante/Intermedio/Contribuitore)
2. **Leggi documenti** secondo ordine consigliato
3. **Esegui setup** locale seguendo PRACTICAL_GUIDE.md
4. **Inizia sviluppo** - Usa template forniti per aggiungere feature
5. **Contribuisci** - Segui pattern nel codebase

---

**Enjoy developing with Memoa! 🚀**
