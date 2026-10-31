import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
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
import { defaultTitle, titleFromFileName, todayInputValue, toOccurredAt } from './note-date';
import { NoteDraft } from './note-draft';

export type NewNoteMode = 'record' | 'write' | 'upload';

/** Contenuto acquisito nel primo passo. */
type Content = { kind: 'audio'; audio: Blob; contentType: string } | { kind: 'text'; text: string };

type Step =
  | { kind: 'capture' }
  | { kind: 'details' }
  | { kind: 'saving'; fraction: number | null }
  | { kind: 'failed' };

/**
 * Creazione di una nota in due passi: prima la registrazione, la scrittura o la scelta del file, poi
 * titolo e data della nota. Per gli audio titolo e data vanno con la richiesta del SAS di upload.
 */
@Component({
  selector: 'app-new-note',
  imports: [RouterLink, RecordAudio],
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
  protected readonly captureError = signal<string | null>(null);

  protected readonly text = signal('');
  protected readonly title = signal('');
  protected readonly date = signal(todayInputValue());
  protected readonly titlePlaceholder = computed(() =>
    defaultTitle(this.date() || todayInputValue()),
  );

  protected readonly maxTextLength = MAX_TEXT_LENGTH;
  protected readonly uploadAccept = `${AUDIO_ACCEPT},${TEXT_ACCEPT}`;
  protected readonly formats = `${AUDIO_FORMATS_LABEL}, ${TEXT_FORMATS_LABEL}`;
  protected readonly heading = computed(
    () => ({ record: 'Registra', write: 'Scrivi', upload: 'Carica un file' })[this.mode()],
  );

  // Dati dell'ultimo tentativo, per riprovare senza ripetere i passi.
  private note?: NoteDetails;

  ngOnInit(): void {
    if (this.mode() === 'upload') {
      const file = this.drafts.take();
      if (file) {
        void this.selectFile(file);
      }
    }
  }

  protected onRecorded(audio: Blob): void {
    this.content.set({ kind: 'audio', audio, contentType: audio.type });
    this.step.set({ kind: 'details' });
  }

  protected continueWithText(): void {
    const text = this.text();
    if (text.trim() && text.length <= MAX_TEXT_LENGTH) {
      this.content.set({ kind: 'text', text });
      this.step.set({ kind: 'details' });
    }
  }

  protected onFileInput(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (file) {
      void this.selectFile(file);
    }
  }

  protected async selectFile(file: File): Promise<void> {
    this.captureError.set(null);

    if (isTextFile(file)) {
      const result = await readTextFile(file);
      if ('error' in result) {
        this.captureError.set(result.error);
        return;
      }
      this.content.set({ kind: 'text', text: result.text });
    } else {
      const contentType = audioContentType(file);
      if (!contentType) {
        this.captureError.set(`Formato non supportato. Formati ammessi: ${this.formats}.`);
        return;
      }
      if (file.size === 0) {
        this.captureError.set('Il file è vuoto.');
        return;
      }
      if (file.size > MAX_AUDIO_SIZE_BYTES) {
        this.captureError.set('Il file supera il limite di 350 MB.');
        return;
      }
      this.content.set({ kind: 'audio', audio: file, contentType });
    }

    this.title.set(titleFromFileName(file.name));
    this.step.set({ kind: 'details' });
  }

  protected save(): void {
    const date = this.date() || todayInputValue();
    this.note = {
      title: this.title().trim() || defaultTitle(date),
      occurredAt: toOccurredAt(date),
    };
    this.submit();
  }

  /**
   * Invia con i dati dell'ultimo salvataggio. Per l'audio ogni tentativo crea un nuovo record: quello di
   * un upload fallito viene eliminato da AudioUploadService.
   */
  protected submit(): void {
    const content = this.content();
    const note = this.note;
    if (!content || !note) {
      return;
    }

    if (content.kind === 'text') {
      this.step.set({ kind: 'saving', fraction: null });
      this.api
        .createText({ ...note, text: content.text })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => void this.router.navigate(['/']),
          error: (err) => this.fail(err),
        });
      return;
    }

    this.step.set({ kind: 'saving', fraction: 0 });
    this.uploads
      .upload(content.audio, content.contentType, note)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (progress) => {
          if (progress.kind === 'progress') {
            this.step.set({ kind: 'saving', fraction: progress.fraction });
          } else {
            void this.router.navigate(['/']);
          }
        },
        error: (err) => this.fail(err),
      });
  }

  private fail(err: unknown): void {
    // Il contenuto resta in memoria per riprovare senza registrare, scrivere o scegliere di nuovo.
    console.error('Salvataggio della nota non riuscito', err);
    this.step.set({ kind: 'failed' });
  }
}
