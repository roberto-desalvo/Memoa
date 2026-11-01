import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink } from '@angular/router';
import { lastValueFrom, Observable, tap } from 'rxjs';
import { MAX_TEXT_LENGTH, NoteDetails, RecordsApi } from '../records/records-api';
import { RecordAudio } from '../recording/record-audio';
import {
  AUDIO_ACCEPT,
  AUDIO_FORMATS_LABEL,
  audioContentType,
  MAX_AUDIO_SIZE_BYTES,
} from '../uploads/audio-formats';
import { AudioUploadService } from '../uploads/audio-upload.service';
import { isTextFile, readTextFile, TEXT_ACCEPT, TEXT_FORMATS_LABEL } from '../uploads/text-file';
import {
  nowInputValue,
  recordingTitle,
  textTitle,
  titleFromFileName,
  toOccurredAt,
} from './note-date';
import { NoteDraft } from './note-draft';

export type NewNoteMode = 'record' | 'write' | 'upload';

/** Contenuto acquisito nel primo passo. */
type Content = { kind: 'audio'; audio: Blob; contentType: string } | { kind: 'text'; text: string };

type Step =
  | { kind: 'capture' }
  | { kind: 'details' }
  | { kind: 'saving'; fraction: number | null }
  | { kind: 'failed' }
  | { kind: 'files' };

/** File scelto per l'upload, salvato come nota a sé. */
interface FileItem {
  file: File;
  title: string;
  status: 'waiting' | 'saving' | 'done' | 'failed' | 'invalid';
  /** Avanzamento dell'upload audio; null per il testo. */
  fraction: number | null;
  /** Motivo per cui il file non è valido, per l'utente. */
  error: string | null;
}

/**
 * Creazione delle note. La scrittura ha due passi: il testo, poi titolo, data e ora. La registrazione
 * e gli upload si salvano subito: la registrazione con data e ora attuali e il titolo scritto sopra il
 * registratore (proposta REC_yyyyMMdd_HHmm dall'avvio della registrazione), gli upload con la data di ultima modifica del file e come titolo il nome del file; titolo e data sono
 * modificabili poi nella pagina del record. Gli upload accettano più
 * file, salvati uno alla volta come note separate. Per gli audio titolo e data vanno con la richiesta
 * del SAS di upload.
 */
@Component({
  selector: 'app-new-note',
  imports: [
    RouterLink,
    RecordAudio,
    DecimalPipe,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatToolbarModule,
  ],
  templateUrl: './new-note.html',
  styleUrl: './new-note.scss',
  host: { class: 'page' },
})
export class NewNote implements OnInit {
  private readonly router = inject(Router);
  private readonly api = inject(RecordsApi);
  private readonly uploads = inject(AudioUploadService);
  private readonly drafts = inject(NoteDraft);
  private readonly destroyRef = inject(DestroyRef);

  /** Dal percorso /new/:mode. */
  readonly mode = input.required<NewNoteMode>();

  protected readonly step = signal<Step>({ kind: 'capture' });
  protected readonly content = signal<Content | null>(null);

  protected readonly text = signal('');
  protected readonly title = signal('');
  protected readonly date = signal(nowInputValue());
  protected readonly titlePlaceholder = computed(() =>
    textTitle(new Date(this.date() || nowInputValue())),
  );

  /** Titolo della registrazione scritto dall'utente; null finché non lo modifica. */
  protected readonly recordTitle = signal<string | null>(null);
  protected readonly recordTitleProposal = signal(recordingTitle(new Date()));

  protected readonly files = signal<FileItem[]>([]);
  protected readonly filesBusy = computed(() =>
    this.files().some((item) => item.status === 'waiting' || item.status === 'saving'),
  );
  protected readonly hasFailedFiles = computed(() =>
    this.files().some((item) => item.status === 'failed'),
  );

  protected readonly maxTextLength = MAX_TEXT_LENGTH;
  protected readonly uploadAccept = `${AUDIO_ACCEPT},${TEXT_ACCEPT}`;
  protected readonly formats = `${AUDIO_FORMATS_LABEL}, ${TEXT_FORMATS_LABEL}`;
  protected readonly heading = computed(
    () => ({ record: 'Registra', write: 'Scrivi', upload: 'Carica file' })[this.mode()],
  );

  // Dati dell'ultimo tentativo, per riprovare senza ripetere i passi.
  private note?: NoteDetails;
  private destroyed = false;

  constructor() {
    this.destroyRef.onDestroy(() => (this.destroyed = true));
  }

  ngOnInit(): void {
    if (this.mode() === 'upload') {
      const files = this.drafts.take();
      if (files.length > 0) {
        this.startFiles(files);
      }
    }
  }

  /** La proposta segue l'avvio della registrazione, finché l'utente non scrive un titolo. */
  protected onRecordingStarted(): void {
    this.recordTitleProposal.set(recordingTitle(new Date()));
  }

  protected onRecorded(audio: Blob): void {
    const now = new Date();
    this.content.set({ kind: 'audio', audio, contentType: audio.type });
    this.note = {
      title: this.recordTitle()?.trim() || this.recordTitleProposal(),
      occurredAt: now.toISOString(),
    };
    void this.submit();
  }

  protected continueWithText(): void {
    const text = this.text();
    if (text.trim() && text.length <= MAX_TEXT_LENGTH) {
      this.content.set({ kind: 'text', text });
      this.step.set({ kind: 'details' });
    }
  }

  protected save(): void {
    const occurredAt = toOccurredAt(this.date() || nowInputValue());
    this.note = {
      title: this.title().trim() || textTitle(new Date(occurredAt)),
      occurredAt,
    };
    void this.submit();
  }

  /**
   * Invia con i dati dell'ultimo salvataggio. Per l'audio ogni tentativo crea un nuovo record: quello di
   * un upload fallito viene eliminato da AudioUploadService.
   */
  protected async submit(): Promise<void> {
    const content = this.content();
    const note = this.note;
    if (!content || !note) {
      return;
    }

    this.step.set({ kind: 'saving', fraction: content.kind === 'audio' ? 0 : null });
    try {
      await this.saveContent(content, note, (fraction) =>
        this.step.set({ kind: 'saving', fraction }),
      );
      if (!this.destroyed) {
        void this.router.navigate(['/']);
      }
    } catch (err) {
      // Il contenuto resta in memoria per riprovare senza registrare o scrivere di nuovo.
      console.error('Salvataggio della nota non riuscito', err);
      this.step.set({ kind: 'failed' });
    }
  }

  protected onFileInput(input: HTMLInputElement): void {
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (files.length > 0) {
      this.startFiles(files);
    }
  }

  protected retryFiles(): void {
    this.files.update((items) =>
      items.map((item) => (item.status === 'failed' ? { ...item, status: 'waiting' } : item)),
    );
    void this.processFiles();
  }

  private startFiles(files: File[]): void {
    this.files.set(
      files.map((file) => ({
        file,
        title: titleFromFileName(file.name),
        status: 'waiting',
        fraction: null,
        error: null,
      })),
    );
    this.step.set({ kind: 'files' });
    void this.processFiles();
  }

  /**
   * Salva i file in attesa uno alla volta, ciascuno come nota con la data di ultima modifica. Un file non
   * valido o non salvato non ferma gli altri; se sono stati salvati tutti si torna alla lista.
   */
  private async processFiles(): Promise<void> {
    for (let index = 0; index < this.files().length; index++) {
      const item = this.files()[index];
      if (item.status !== 'waiting') {
        continue;
      }

      this.updateFile(index, { status: 'saving' });
      const content = await readContent(item.file);
      if ('error' in content) {
        this.updateFile(index, { status: 'invalid', error: content.error });
        continue;
      }

      const occurredAt = fileDate(item.file);
      const note = {
        title: item.title || (content.kind === 'audio' ? recordingTitle : textTitle)(occurredAt),
        occurredAt: occurredAt.toISOString(),
      };
      try {
        await this.saveContent(content, note, (fraction) => this.updateFile(index, { fraction }));
        this.updateFile(index, { status: 'done' });
      } catch (err) {
        console.error('Salvataggio del file non riuscito', err);
        this.updateFile(index, { status: 'failed', fraction: null });
      }
      if (this.destroyed) {
        return;
      }
    }

    if (this.files().every((item) => item.status === 'done')) {
      void this.router.navigate(['/']);
    }
  }

  private updateFile(index: number, changes: Partial<FileItem>): void {
    this.files.update((items) =>
      items.map((item, i) => (i === index ? { ...item, ...changes } : item)),
    );
  }

  /** Crea il record; si interrompe se l'utente lascia la pagina. */
  private async saveContent(
    content: Content,
    note: NoteDetails,
    onProgress: (fraction: number) => void,
  ): Promise<void> {
    const request: Observable<unknown> =
      content.kind === 'text'
        ? this.api.createText({ ...note, text: content.text })
        : this.uploads
            .upload(content.audio, content.contentType, note)
            .pipe(tap((progress) => progress.kind === 'progress' && onProgress(progress.fraction)));
    await lastValueFrom(request.pipe(takeUntilDestroyed(this.destroyRef)));
  }
}

/** Data della nota di un file caricato: l'ultima modifica del file, o adesso se il browser non la fornisce. */
function fileDate(file: File): Date {
  return file.lastModified > 0 ? new Date(file.lastModified) : new Date();
}

/** Contenuto di un file caricato, oppure il messaggio d'errore per l'utente. */
async function readContent(file: File): Promise<Content | { error: string }> {
  if (isTextFile(file)) {
    const result = await readTextFile(file);
    return 'error' in result ? result : { kind: 'text', text: result.text };
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
  return { kind: 'audio', audio: file, contentType };
}
