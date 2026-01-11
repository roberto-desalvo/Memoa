# Documentazione Frontend - Memoa

Documentazione completa, dettagliata e pratica del frontend Memoa.

## 📖 Inizia da qui

**Per prima cosa, leggi [INDEX.md](./INDEX.md)** - Troverai un indice completo, percorsi di lettura consigliati, e come navigare la documentazione.

---

## 📚 Documenti Disponibili

1. **[INDEX.md](./INDEX.md)** ⭐ INIZIO
   - Indice e navigazione
   - Percorsi di lettura consigliati
   - Quick reference

2. **[FRONTEND_DOCUMENTATION.md](./FRONTEND_DOCUMENTATION.md)**
   - Panoramica completa del progetto
   - Stack tecnologico
   - Descrizione dettagliata di ogni componente, hook, servizio
   - Flussi applicazione

3. **[ARCHITECTURE_PATTERNS.md](./ARCHITECTURE_PATTERNS.md)**
   - Architettura generale
   - Pattern di design utilizzati
   - Separazione responsabilità
   - Error handling e performance

4. **[API_FLOWS.md](./API_FLOWS.md)**
   - Integrazione API
   - Client HTTP e servizi
   - Endpoint disponibili
   - Flussi dettagliati di ogni operazione
   - Debugging API

5. **[PRACTICAL_GUIDE.md](./PRACTICAL_GUIDE.md)**
   - Setup e avvio
   - Template per aggiungere componenti/hook/endpoint
   - Guide per modificare codice
   - Debugging guide
   - Troubleshooting

---

## 🎯 In Base a Quello che Cerchi

### Voglio capire come funziona il progetto
→ Inizia con **[FRONTEND_DOCUMENTATION.md](./FRONTEND_DOCUMENTATION.md)**

### Voglio imparare l'architettura e i pattern
→ Leggi **[ARCHITECTURE_PATTERNS.md](./ARCHITECTURE_PATTERNS.md)**

### Voglio aggiungere una nuova feature
→ Segui i template in **[PRACTICAL_GUIDE.md](./PRACTICAL_GUIDE.md)**

### Ho un'errore o problema
→ Consulta **[PRACTICAL_GUIDE.md](./PRACTICAL_GUIDE.md)** - Debugging e Troubleshooting

### Voglio capire come funzionano le API
→ Leggi **[API_FLOWS.md](./API_FLOWS.md)**

---

## 🚀 Quick Start

```bash
# 1. Setup
cd app
npm install
cp .env.example .env.local
# Configura VITE_SUPABASE_URL, VITE_SUPABASE_ANON_KEY, VITE_API_URL

# 2. Avvio
npm run dev
# Visita http://localhost:5173

# 3. Sviluppo
npm run lint          # Esegui linter
npm run build         # Build produzione
npm run preview       # Anteprima build
```

---

## 📋 Sommario Contenuto

### FRONTEND_DOCUMENTATION.md (~20 KB)
- Panoramica progetto
- Stack tecnologico (React, TypeScript, Tailwind, Supabase)
- Struttura file dettagliata
- 30+ componenti, hook, servizi descritti
- Autenticazione passwordless
- Audio recording e storage
- Gestione stato
- Flusso applicazione completo

### ARCHITECTURE_PATTERNS.md (~15 KB)
- Architettura a layer
- Pattern stato (Context, Hooks, Local)
- Pattern componenti (Page, Feature, Presentational)
- Flusso dati unidirezionale
- Separazione responsabilità per feature
- Error handling multi-layer
- Performance considerations
- Testing strategy

### API_FLOWS.md (~18 KB)
- Architettura API
- Client HTTP generico e service domain
- 5+ endpoint dettagliati (GET, POST, DELETE)
- Flussi operazioni (load, create, delete, search)
- Autenticazione Bearer JWT
- Supabase Auth + Storage
- Debugging e monitoring

### PRACTICAL_GUIDE.md (~16 KB)
- Setup iniziale passo per passo
- Template: aggiungere componente presentazionale
- Template: aggiungere componente con logica
- Template: aggiungere hook semplice
- Template: aggiungere hook complesso
- Template: aggiungere nuovo endpoint
- Debugging guide con scenari
- Checklist sviluppo
- Troubleshooting 10+ errori comuni
- Deploy Vercel/Netlify

---

## 🎓 Concetti Principali Documentati

**Architettura**
- Layer stack (UI → Hooks → Services → External)
- Separazione componenti (Page, Feature, Presentational)
- Flusso dati unidirezionale

**Stato**
- Context per global state
- Custom hooks per business logic
- Component state per UI

**Autenticazione**
- Magic link passwordless
- JWT token handling
- Session management
- Auto-logout su 401

**API**
- Client HTTP con auth automatica
- Service pattern per domain logic
- Error handling standardizzato
- Typed responses con TypeScript

**Audio**
- Registrazione da microfono
- Upload a Supabase Storage
- Signed URL per accesso
- Metadati tracciati

**UI**
- Tailwind CSS con dark mode
- Responsive design
- Loading/error states
- User feedback

---

## ✅ Cosa è Documentato

- ✅ Struttura del progetto
- ✅ Stack tecnologico
- ✅ Ogni componente React
- ✅ Ogni hook personalizzato
- ✅ Servizi API
- ✅ Autenticazione
- ✅ Storage e file audio
- ✅ Gestione tema
- ✅ Routing
- ✅ Architettura generale
- ✅ Pattern di design
- ✅ Error handling
- ✅ Performance
- ✅ Testing strategy
- ✅ Flussi operazioni
- ✅ Guide pratiche
- ✅ Template aggiungere feature
- ✅ Debugging guide
- ✅ Troubleshooting

---

## 📱 Utilizzo Documentazione

### Nel Browser
1. Apri INDEX.md
2. Leggi sommario e percorsi di lettura
3. Naviga ai documenti interessanti
4. Usa Ctrl+F per cercare parole chiave

### In VS Code
1. Apri cartella docs
2. Usa "Preview" per visualizzare Markdown
3. Usa "Go to Definition" (Ctrl+G) per saltare tra file
4. Usa "Find in Folder" (Ctrl+Shift+F) per ricercare

### Su GitHub
1. Cartella `/docs` nel repository
2. GitHub renderizza automaticamente Markdown
3. Clicca link per navigare tra documenti

---

## 🔍 Ricerca Veloce

**Cerco informazione su...**

```
entryForm     → FRONTEND_DOCUMENTATION → useEntryForm.ts
audio         → FRONTEND_DOCUMENTATION → useAudioRecorder.ts
API           → API_FLOWS.md
login         → FRONTEND_DOCUMENTATION → AuthProvider.tsx
hook          → ARCHITECTURE_PATTERNS → Pattern di Stato
componente    → FRONTEND_DOCUMENTATION → Componenti React
errore        → PRACTICAL_GUIDE → Troubleshooting
deploy        → PRACTICAL_GUIDE → Deployment
testing       → ARCHITECTURE_PATTERNS → Testing Strategy
```

---

## 🎯 Per Ruolo

### Developer Principiante
1. Leggi INDEX.md (10 min)
2. Leggi FRONTEND_DOCUMENTATION.md (45 min)
3. Leggi ARCHITECTURE_PATTERNS.md (30 min)
4. Setup locale con PRACTICAL_GUIDE.md (15 min)
5. Tempo totale: ~2 ore

### Developer Intermedio
1. Ripassi FRONTEND_DOCUMENTATION.md sezioni specifiche (20 min)
2. Consulta ARCHITECTURE_PATTERNS.md per pattern (10 min)
3. Usa PRACTICAL_GUIDE.md per feature nuove (variabile)
4. Usa API_FLOWS.md per API (variabile)
5. Tempo totale: ~30 min + sviluppo

### DevOps / Deployment
1. Leggi sezione "Deployment" in PRACTICAL_GUIDE.md (5 min)
2. Consulta env vars necessari (INDEX.md - Quick Reference)
3. Tempo totale: ~10 min

### QA / Tester
1. Leggi FRONTEND_DOCUMENTATION.md per capire features (30 min)
2. Leggi PRACTICAL_GUIDE.md - Checklist Sviluppo (10 min)
3. Usa per test manual e identificare bug
4. Tempo totale: ~40 min

---

## 📊 Statistiche Documentazione

| Metrica | Valore |
|---|---|
| Documenti | 5 file Markdown |
| Righe di documentazione | ~3500+ |
| Componenti documentati | 25+ |
| Hook documentati | 8+ |
| Servizi documentati | 3+ |
| Endpoint documentati | 5+ |
| Code examples | 100+ |
| Diagram e flowchart | 20+ |
| Sezioni troubleshooting | 10+ |
| Template aggiungere feature | 5+ |

---

## 🆘 Help & Support

1. **Domanda generale**: Consulta INDEX.md → Come Trovare Cose
2. **Errore tecnico**: PRACTICAL_GUIDE.md → Troubleshooting Comune
3. **Architettura**: ARCHITECTURE_PATTERNS.md
4. **API issue**: API_FLOWS.md → Debugging
5. **Feature nuova**: PRACTICAL_GUIDE.md → Template

---

## 📝 Note Importanti

- 📌 Leggi **INDEX.md PRIMA** di altri documenti
- 📌 Documenti sono **cross-referenced** - segui link
- 📌 Usa **Ctrl+F** per cercare parole chiave
- 📌 Codice examples sono **copy-paste ready**
- 📌 Template forniti **seguono pattern del progetto**
- 📌 Aggiorna docs quando aggiungi feature

---

## 🚀 Prossimi Passi

1. **Leggi [INDEX.md](./INDEX.md)** per navigare
2. **Scegli percorso di lettura** basato su tuo ruolo
3. **Setup locale** seguendo PRACTICAL_GUIDE.md
4. **Inizia sviluppo** - Usa template forniti
5. **Contribuisci** - Mantieni documentazione updated

---

**Documentazione creata il: 11 Gennaio 2026**
**Versione: 1.0**
**Ultimo update: 11 Gennaio 2026**

---

**Buono sviluppo! 🎉**
