import { computed, inject, Injectable, signal } from '@angular/core';
import { Observable, Subscription, tap } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import {
  inputValueOf,
  nowInputValue,
  recordingTitle,
  textTitle,
  titleFromFileName,
  toOccurredAt,
} from '../notes/note-date';
import { NoteDetails } from '../records/records-api';
import { RecordsStore } from '../records/records-store';
import { audioContentType, MAX_AUDIO_SIZE_BYTES } from './audio-formats';
import { exceedsMaxDuration } from './audio-duration';
import { AudioUploadService } from './audio-upload.service';
import { checkTextFile, textContentType } from './text-file';

/** File già controllato, con il tipo MIME da salvare sul blob. */
interface FileContent {
  kind: 'audio' | 'text';
  file: Blob;
  contentType: string;
}

export type UploadStatus =
  'checking' | 'preview' | 'invalid' | 'waiting' | 'saving' | 'done' | 'failed';

/** File scelto per l'upload, salvato come nota a sé. */
export interface UploadItem {
  id: number;
  file: File;
  kind: 'audio' | 'text' | null;
  /** Titolo scritto nell'anteprima; vuoto per la proposta REC/TXT dalla data. */
  title: string;
  /** Data e ora della nota (valore dell'input datetime-local). */
  date: string;
  status: UploadStatus;
  /** Avanzamento dell'upload; null finché non parte. */
  fraction: number | null;
  /** Motivo per cui il file non è valido, per l'utente. */
  error: string | null;
}

/**
 * Upload dei file scelti con "Carica". Ogni file passa prima da un'anteprima con titolo (il nome del
 * file) e data (l'ultima modifica), modificabili, senza chiamare le Functions; confermati, i file si
 * salvano uno alla volta come note separate. La coda sta in memoria, fuori dalle pagine: gli upload
 * continuano cambiando pagina e si perdono chiudendo l'app.
 */
@Injectable({ providedIn: 'root' })
export class UploadQueue {
  private readonly uploads = inject(AudioUploadService);
  private readonly store = inject(RecordsStore);
  private readonly maxAudioDurationMinutes = inject(APP_CONFIG).maxAudioDurationMinutes;

  readonly items = signal<UploadItem[]>([]);

  readonly preview = computed(() => this.items().filter((item) => isPreview(item.status)));
  readonly queued = computed(() => this.items().filter((item) => !isPreview(item.status)));
  readonly pendingCount = computed(
    () =>
      this.queued().filter((item) => item.status === 'waiting' || item.status === 'saving').length,
  );
  readonly failedCount = computed(
    () => this.queued().filter((item) => item.status === 'failed').length,
  );

  // Contenuto letto nell'anteprima e upload in corso, per file.
  private readonly contents = new Map<number, FileContent>();
  private readonly running = new Map<number, Subscription>();
  private nextId = 0;
  private processing = false;

  /** Aggiunge i file all'anteprima e li controlla (formato, dimensione, durata). */
  add(files: File[]): void {
    const added = files.map((file): UploadItem => ({
      id: this.nextId++,
      file,
      kind: null,
      title: titleFromFileName(file.name),
      date: inputValueOf(fileDate(file).toISOString()),
      status: 'checking',
      fraction: null,
      error: null,
    }));
    this.items.update((items) => [...items, ...added]);

    for (const item of added) {
      void readContent(item.file, this.maxAudioDurationMinutes).then((content) => {
        // Anteprima annullata durante il controllo.
        if (!this.items().some((i) => i.id === item.id)) {
          return;
        }
        if ('error' in content) {
          this.update(item.id, { status: 'invalid', error: content.error });
        } else {
          this.contents.set(item.id, content);
          this.update(item.id, { status: 'preview', kind: content.kind });
        }
      });
    }
  }

  edit(id: number, changes: Pick<Partial<UploadItem>, 'title' | 'date'>): void {
    this.update(id, changes);
  }

  /** Salva i file validi dell'anteprima; quelli non validi escono dalla lista. */
  confirm(): void {
    this.items.update((items) =>
      items
        .filter((item) => item.status !== 'invalid')
        .map((item) => (item.status === 'preview' ? { ...item, status: 'waiting' } : item)),
    );
    void this.process();
  }

  /** Toglie l'anteprima; gli upload confermati continuano. */
  discardPreview(): void {
    for (const item of this.preview()) {
      this.contents.delete(item.id);
    }
    this.items.update((items) => items.filter((item) => !isPreview(item.status)));
  }

  /**
   * Toglie un file dalla coda. Un upload in corso si interrompe e il suo record viene eliminato
   * (AudioUploadService).
   */
  cancel(id: number): void {
    if (!this.items().some((i) => i.id === id)) {
      return;
    }
    this.running.get(id)?.unsubscribe();
    this.contents.delete(id);
    this.items.update((items) => items.filter((i) => i.id !== id));
  }

  retryFailed(): void {
    this.items.update((items) =>
      items.map((item) => (item.status === 'failed' ? { ...item, status: 'waiting' } : item)),
    );
    void this.process();
  }

  /** Toglie i file salvati e quelli non riusciti, se nulla è in corso. */
  clearFinished(): void {
    if (this.pendingCount() > 0) {
      return;
    }
    for (const item of this.queued()) {
      this.contents.delete(item.id);
    }
    this.items.update((items) => items.filter((item) => isPreview(item.status)));
  }

  /**
   * Salva i file in attesa uno alla volta, ciascuno come nota. Un file non salvato non ferma gli altri;
   * se sono stati salvati tutti escono dalla lista.
   */
  private async process(): Promise<void> {
    if (this.processing) {
      return;
    }
    this.processing = true;
    try {
      let item: UploadItem | undefined;
      while ((item = this.items().find((i) => i.status === 'waiting'))) {
        await this.save(item);
      }
    } finally {
      this.processing = false;
    }

    if (this.failedCount() === 0) {
      this.clearFinished();
    }
  }

  /** Si conclude anche quando l'utente annulla l'upload. */
  private save(item: UploadItem): Promise<void> {
    const content = this.contents.get(item.id);
    if (!content) {
      this.update(item.id, { status: 'failed' });
      return Promise.resolve();
    }

    // I secondi dell'ultima modifica del file mantengono l'ordine tra file dello stesso minuto.
    const occurredAt = toOccurredAt(item.date || nowInputValue(), fileDate(item.file));
    const note: NoteDetails = { title: item.title.trim() || proposedTitle(item), occurredAt };
    const kind = content.kind === 'text' ? 'document' : 'audio';
    const request: Observable<unknown> = this.uploads
      .upload(content.file, content.contentType, note, kind)
      .pipe(
        tap(
          (progress) =>
            progress.kind === 'progress' && this.update(item.id, { fraction: progress.fraction }),
        ),
      );

    this.update(item.id, { status: 'saving', fraction: 0 });
    return new Promise((resolve) => {
      const subscription = request.subscribe({
        complete: () => {
          this.update(item.id, { status: 'done' });
          void this.store.refresh();
        },
        error: (err: unknown) => {
          console.error('Salvataggio del file non riuscito', err);
          this.update(item.id, { status: 'failed', fraction: null });
        },
      });
      subscription.add(() => {
        this.running.delete(item.id);
        resolve();
      });
      if (!subscription.closed) {
        this.running.set(item.id, subscription);
      }
    });
  }

  private update(id: number, changes: Partial<UploadItem>): void {
    this.items.update((items) =>
      items.map((item) => (item.id === id ? { ...item, ...changes } : item)),
    );
  }
}

/** Titolo usato se l'utente lascia vuoto il campo: REC/TXT con la data della nota. */
export function proposedTitle(item: UploadItem): string {
  return (item.kind === 'text' ? textTitle : recordingTitle)(
    new Date(item.date || nowInputValue()),
  );
}

function isPreview(status: UploadStatus): boolean {
  return status === 'checking' || status === 'preview' || status === 'invalid';
}

/** Data della nota di un file caricato: l'ultima modifica del file, o adesso se il browser non la fornisce. */
function fileDate(file: File): Date {
  return file.lastModified > 0 ? new Date(file.lastModified) : new Date();
}

/** Contenuto di un file caricato, oppure il messaggio d'errore per l'utente. */
async function readContent(
  file: File,
  maxDurationMinutes: number,
): Promise<FileContent | { error: string }> {
  const textType = textContentType(file);
  if (textType) {
    const error = await checkTextFile(file, textType);
    return error ? { error } : { kind: 'text', file, contentType: textType };
  }

  const contentType = audioContentType(file);
  if (!contentType) {
    return { error: 'Formato non supportato.' };
  }
  if (file.size === 0) {
    return { error: 'Il file è vuoto.' };
  }
  if (file.size > MAX_AUDIO_SIZE_BYTES) {
    return { error: 'Il file supera il limite di 350 MB.' };
  }
  if (await exceedsMaxDuration(file, maxDurationMinutes)) {
    return { error: `L'audio supera la durata massima di ${maxDurationMinutes} minuti.` };
  }
  return { kind: 'audio', file, contentType };
}
