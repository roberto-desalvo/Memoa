import {
  HttpClient,
  HttpErrorResponse,
  HttpEvent,
  HttpEventType,
  HttpHeaders,
  HttpUploadProgressEvent,
} from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  catchError,
  concat,
  filter,
  finalize,
  map,
  Observable,
  of,
  retry,
  switchMap,
  tap,
  throwError,
  timer,
} from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { NoteDetails } from '../records/records-api';

/** Record creato dalle Functions e URL con SAS per caricarne il file. */
interface UploadTarget {
  recordUid: string;
  uploadUrl: string;
  expiresOn: string;
}

/** Audio (normalizzato e trascritto) o file di testo (PDF, MD, TXT, da cui si estrae il testo). */
export type UploadKind = 'audio' | 'document';

export type UploadProgress =
  { kind: 'progress'; fraction: number } | { kind: 'done'; recordUid: string };

// Sopra questa soglia il file va a blocchi: un errore di rete costa un blocco, non l'intero file.
const BLOCK_SIZE = 8 * 1024 * 1024;
const BLOCK_RETRIES = 3;

/**
 * Upload diretto su Blob Storage, per l'audio e per i file di testo: le Functions creano il record "in
 * elaborazione" con titolo e data della nota e restituiscono un URL con SAS valido solo per creare quel
 * blob, poi il file va allo storage senza passare dalle Functions.
 */
@Injectable({ providedIn: 'root' })
export class AudioUploadService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(APP_CONFIG);

  /**
   * `contentType` è il tipo MIME salvato sul blob (per i file di testo sceglie anche il limite di
   * dimensione). Se l'upload fallisce il record appena creato viene eliminato, così non resta "in
   * elaborazione" senza file: un nuovo tentativo crea un altro record.
   */
  upload(
    audio: Blob,
    contentType: string,
    note: NoteDetails,
    kind: UploadKind = 'audio',
  ): Observable<UploadProgress> {
    const type = contentType || 'application/octet-stream';

    // Il token di accesso lo aggiunge MsalInterceptor; l'URL dello storage non è tra le sue
    // risorse protette, quindi le richieste allo storage partono solo con il SAS.
    return this.http
      .post<UploadTarget>(`${this.config.apiBaseUrl}/uploads/${kind}`, {
        sizeBytes: audio.size,
        contentType: type,
        title: note.title,
        occurredAt: note.occurredAt,
      })
      .pipe(
        switchMap(({ recordUid, uploadUrl }) => {
          // Interrotto dall'utente (disiscrizione) prima della fine: il record va eliminato.
          let settled = false;
          return concat(
            audio.size <= BLOCK_SIZE
              ? this.putBlob(uploadUrl, audio, type)
              : this.putBlocks(uploadUrl, audio, type),
            [{ kind: 'done', recordUid } as const],
          ).pipe(
            tap({ complete: () => (settled = true), error: () => (settled = true) }),
            catchError((error: unknown) => this.cancel(kind, recordUid, error)),
            finalize(() => settled || this.abandon(kind, recordUid)),
          );
        }),
      );
  }

  /**
   * Elimina il record di un upload annullato dall'utente, senza attendere la risposta. Il 409 (file
   * arrivato comunque) lascia la nota com'è. Se l'utente annulla prima della risposta con il SAS il
   * record lo elimina la coda `upload-expiry`.
   */
  private abandon(kind: UploadKind, recordUid: string): void {
    this.http.delete(`${this.config.apiBaseUrl}/uploads/${kind}/${recordUid}`).subscribe({
      error: (error: unknown) => {
        if (!(error instanceof HttpErrorResponse && error.status === 409)) {
          console.error("Eliminazione del record dopo l'upload annullato non riuscita", error);
        }
      },
    });
  }

  /**
   * Elimina il record di un upload fallito e ripropaga l'errore dell'upload. Le Functions rispondono 409
   * se il file risulta arrivato comunque (es. risposta persa dopo il commit): in quel caso l'upload è
   * riuscito. Se anche l'eliminazione fallisce il record resta in elaborazione.
   */
  private cancel(
    kind: UploadKind,
    recordUid: string,
    uploadError: unknown,
  ): Observable<UploadProgress> {
    return this.http.delete(`${this.config.apiBaseUrl}/uploads/${kind}/${recordUid}`).pipe(
      map(() => false),
      catchError((error: unknown) => {
        const completed = error instanceof HttpErrorResponse && error.status === 409;
        if (!completed) {
          console.error("Eliminazione del record dopo l'upload fallito non riuscita", error);
        }
        return of(completed);
      }),
      switchMap((completed) =>
        completed ? of({ kind: 'done', recordUid } as const) : throwError(() => uploadError),
      ),
    );
  }

  // Put Blob in una sola richiesta.
  private putBlob(uploadUrl: string, audio: Blob, contentType: string): Observable<UploadProgress> {
    const headers = new HttpHeaders({
      'x-ms-blob-type': 'BlockBlob',
      'x-ms-blob-content-type': contentType,
    });

    return this.http
      .put(uploadUrl, audio, { headers, observe: 'events', reportProgress: true })
      .pipe(progressOf(0, audio.size));
  }

  // Put Block per ogni blocco, in sequenza, poi Put Block List che crea il blob: finché la lista
  // non è confermata il blob non esiste e non parte nessun evento "blob creato".
  private putBlocks(
    uploadUrl: string,
    audio: Blob,
    contentType: string,
  ): Observable<UploadProgress> {
    const blockIds: string[] = [];
    const blocks: Observable<UploadProgress>[] = [];

    for (let offset = 0; offset < audio.size; offset += BLOCK_SIZE) {
      // Gli ID dei blocchi devono avere tutti la stessa lunghezza.
      const blockId = btoa(blockIds.length.toString().padStart(6, '0'));
      blockIds.push(blockId);

      const chunk = audio.slice(offset, offset + BLOCK_SIZE);
      const url = `${uploadUrl}&comp=block&blockid=${encodeURIComponent(blockId)}`;
      blocks.push(
        this.http
          .put(url, chunk, { observe: 'events', reportProgress: true })
          .pipe(
            retry({ count: BLOCK_RETRIES, delay: retryTransient }),
            progressOf(offset, audio.size),
          ),
      );
    }

    const blockList =
      '<?xml version="1.0" encoding="utf-8"?><BlockList>' +
      blockIds.map((id) => `<Latest>${id}</Latest>`).join('') +
      '</BlockList>';
    const commit = this.http
      .put(`${uploadUrl}&comp=blocklist`, blockList, {
        headers: new HttpHeaders({
          'Content-Type': 'application/xml',
          'x-ms-blob-content-type': contentType,
        }),
      })
      .pipe(map((): UploadProgress => ({ kind: 'progress', fraction: 1 })));

    return concat(...blocks, commit);
  }
}

/** Avanzamento sull'intero file di una richiesta che invia i byte da `offset` in poi. */
function progressOf(offset: number, total: number) {
  return (events: Observable<HttpEvent<unknown>>): Observable<UploadProgress> =>
    events.pipe(
      filter(
        (event): event is HttpUploadProgressEvent => event.type === HttpEventType.UploadProgress,
      ),
      map((event) => ({ kind: 'progress', fraction: (offset + event.loaded) / total })),
    );
}

/** Riprova solo errori di rete e del server; SAS scaduto o richiesta rifiutata no. */
function retryTransient(error: unknown, attempt: number): Observable<number> {
  const transient =
    error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500);
  return transient ? timer(attempt * 1000) : throwError(() => error);
}
