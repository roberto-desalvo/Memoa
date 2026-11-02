# Infrastruttura e operatività

[← Indice del progetto](../project.md)

Infrastruttura come codice, ambienti, pipeline, monitoraggio e costi. Comandi e passi manuali sono in [infra/README.md](../../infra/README.md).

Contenuti: [Infrastruttura come codice](#infrastruttura-come-codice) · [Ambienti](#ambienti) · [Pipeline](#pipeline) · [Monitoraggio e log](#monitoraggio-e-log) · [Controllo dei costi](#controllo-dei-costi) · [Dominio ed email](#dominio-ed-email) · [Appendice: alternative scartate](#appendice-alternative-scartate)

---

## Infrastruttura come codice

**Decisione**: Bicep per tutto ciò che lo supporta. Il resto è descritto come passi manuali in [infra/README.md](../../infra/README.md#passi-manuali).

- **Deploy a livello di sottoscrizione** (`main.bicep` + `main.<env>.bicepparam`), uno per ambiente, con `az deployment sub create`. È incrementale: le risorse e i ruoli tolti dal codice non vengono eliminati in automatico. Anteprima con `what-if`.
- **Budget** (`budget.bicep`): deploy separato, unico per la sottoscrizione.
- **Registrazioni delle app di Entra External ID** (`identity/main.bicep`): estensione Microsoft Graph con `targetScope = 'tenant'`, un deploy per tenant esterno (`identity/deploy.ps1`). I tenant esterni non contengono sottoscrizioni. Il Bicep per Graph non rileva eliminazioni né modifiche fatte a mano.
- **Fuori da Bicep**:
  - server SQL condiviso e utenti del database (T-SQL);
  - creazione dei tenant esterni, flussi utente e branding (da ripetere per dev e prod);
  - environment e variabili di GitHub.
- **Sottoscrizioni Event Grid**: si creano solo dopo il primo deploy del codice delle Functions (`deployEventSubscriptions`).
- I valori da non pubblicare (server SQL, token GHCR, authority e audience) arrivano da variabili d'ambiente (`infra/.env`, escluso da git).
- I gruppi di risorse dei tenant esterni non vanno mai inclusi nei deployment stacks, se in futuro si adottano.

---

## Ambienti

**Decisione**: dev e prod nella stessa sottoscrizione, ciascuno nel proprio resource group (`rg-memoa-<env>`) e con il proprio tenant Entra External ID.

- I grant gratuiti di Functions e Container Apps sono per sottoscrizione, quindi condivisi tra dev e prod.
- Ogni ambiente usa uno dei database SQL gratuiti, con la propria quota.
- Un solo budget copre entrambi gli ambienti.

---

## Pipeline

**Decisione**: GitHub Actions, una pipeline per componente. Push su `main` → dev, push su `prod` → prod, con approvazione obbligatoria sull'environment `prod`. È un assetto temporaneo, in attesa del primo rilascio in produzione.

| Workflow | Componente |
|---|---|
| `web.yml` | frontend su Static Web Apps (`config.json` generato per ambiente) |
| `functions.yml` | Functions (migration escluse), con la build statica di ffmpeg nel pacchetto |
| `schema.yml` | script idempotente delle migration, applicato con `sqlcmd` ([dati.md](dati.md#accesso-ai-dati-e-migration)) |
| `transcriber.yml` | immagine del job di trascrizione su GHCR e aggiornamento del job |

- Autenticazione su Azure con OIDC e l'identità `id-memoa-<env>-deploy`, nessun segreto ([sicurezza-identita.md](sicurezza-identita.md#identità-gestite)).
- Schema e Functions si distribuiscono separatamente, in ordine non garantito: le modifiche allo schema vanno fatte in due tempi.
- Le immagini hanno due tag: lo SHA del commit, usato dal job, e il nome del branch, usato dal deploy dell'infrastruttura. Nessun ripiego silenzioso su un'immagine segnaposto: se manca l'immagine del motore scelto si ferma il deploy dell'infrastruttura, se l'immagine del job non corrisponde a nessuna di quelle pubblicate si ferma la pipeline del job.

---

## Monitoraggio e log

**Decisione**: OpenTelemetry + Application Insights basato su workspace Log Analytics, entro le soglie gratuite.

- **Costo**: primi 5 GB al mese gratuiti, poi circa 2,30 $/GB.
- **Limite giornaliero**: 60 MB in dev e 100 MB in prod, in totale circa 5 GB al mese. Superato il limite, i log del resto della giornata vanno persi.
- **Conservazione**: 30 giorni, entro i 31 gratuiti.
- **Accesso**: telemetria inviata solo con Entra ID (ruolo Monitoring Metrics Publisher a Functions e job).
- **Dati personali**:
  - nei log solo identificativi, mai contenuti dei record, trascrizioni, email o token;
  - IP masking attivo su Application Insights;
  - `SensitiveDataProcessor` (in `src/shared`) toglie dalle tracce indirizzi IP, query string degli URL, testo delle query SQL ed `enduser.id`.
- **Livelli di log delle Functions** (`host.json`): `Warning` in generale, `Information` per il codice delle funzioni.

---

## Controllo dei costi

**Decisione**: budget mensile di 10 € sulla sottoscrizione, avvisi email al 50%, 80% e 100% della spesa effettiva e al 100% della spesa prevista a fine mese.

- I budget inviano solo notifiche, non fermano le risorse.
- Per reazioni automatiche si può collegare un action group (`actionGroupId`, opzionale, oggi non usato).

---

## Dominio ed email

**Decisione**: dominio gratuito di Azure Static Web Apps. Le email di registrazione e reset password le invia Entra External ID, quindi non serve un servizio email proprio. Dominio personalizzato da rivalutare più avanti.

---

## Appendice: alternative scartate

**Infrastruttura come codice**: Terraform, OpenTofu, Pulumi, Azure Developer CLI. Bicep non ha file di stato da custodire e supporta subito le nuove risorse Azure.

**Registrazioni delle app**: script Azure CLI (`az ad app` + `az rest`), script Microsoft Graph PowerShell.

**Ambienti**: solo produzione; sottoscrizioni separate, che raddoppierebbero i grant gratuiti ma complicherebbero budget e gestione.

**Pipeline**: Azure DevOps Pipelines, deploy manuale.

**Monitoraggio**: solo i log della piattaforma.

**Dominio**: dominio proprio, con un piccolo costo annuale.
