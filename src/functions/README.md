# Functions

Backend di Memoa: Azure Functions .NET isolated (piano Flex Consumption in Azure).

Configurazione, avvio in locale e migration del database: vedi [docs/debug/sviluppo-locale.md](../../docs/debug/sviluppo-locale.md). Simulazione degli eventi Event Grid: vedi [docs/debug/simulazione-eventi.md](../../docs/debug/simulazione-eventi.md).

## Normalizzazione audio (`NormalizeRecord`)

Attivata dalla coda `normalization`. Scarica l'originale da `uploads`, controlla il formato reale e scrive in `audio` la copia `<oid>/<RecordUid>.opus` (metadati `durationseconds` e `sourceformat`) oppure l'esito `<oid>/<RecordUid>.error.json` (codici in [Normalization/AudioValidation.cs](Normalization/AudioValidation.cs)). Il flusso prosegue dall'evento "blob creato" (`EnqueueTranscription`). Non accede al database.

- **ffmpeg**: la pipeline `functions.yml` scarica la build statica Linux x64 da [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static) (release e SHA-256 fissati nel workflow) e la copia in `tools/ffmpeg/` nel pacchetto, con la licenza. Non c'è ffprobe: il formato, gli stream e la durata si leggono dal riepilogo che `ffmpeg -i` scrive su stderr. Quel testo può contenere i metadati del file, quindi non va mai nei log.
- **Permesso di esecuzione**: se il pacchetto lo perde, al primo uso ffmpeg viene copiato nella cartella temporanea e reso eseguibile.
- **Una normalizzazione alla volta per istanza**: lo spazio temporaneo di Flex Consumption è di 0,8 GB.
- **Retry**: dopo `maxDequeueCount` tentativi (host.json) scrive l'esito `processing_failed` e il messaggio finisce nella coda dei messaggi non elaborabili.
- **In locale**: ffmpeg nel `PATH`, oppure `Ffmpeg__Path`.
