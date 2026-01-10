# Environment Configuration Guide

## Setup Locale

### 1. Configurazione File `.env.local`

Copia `.env.example` in `.env.local` e configura gli indirizzi dei servizi:

```bash
cp .env.example .env.local
```

### 2. Variabili Disponibili

- **VITE_API_URL**: URL base per le API (default: `http://localhost:3000/api`)
- **VITE_ENV**: Environment corrente (default: `development`)

### 3. Accesso alle Variabili nel Codice

```typescript
// Import meta è l'API nativa di Vite
const apiUrl = import.meta.env.VITE_API_URL;
```

### 4. Build Modes

- **Development** (npm run dev): Carica `.env.local`
- **Production** (npm run build): Carica `.env.production` se esiste

### 5. Best Practices

✅ **DO**:
- Usare prefisso `VITE_` per le variabili pubbliche
- Aggiungere `.env.local` a `.gitignore`
- Documentare tutte le variabili in `.env.example`
- Usare `import.meta.env` per l'accesso

❌ **DON'T**:
- Committare `.env.local` (contiene configurazioni locali)
- Usare variabili senza prefisso `VITE_` (non saranno disponibili)
- Mettere secrets in `.env` (usare server-side per quelli sensibili)

### 6. Integrazione API

Quando modifichi l'URL API, il servizio `entriesService.ts` lo userà automaticamente:

```typescript
// In entriesService.ts
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:3000/api';
```
