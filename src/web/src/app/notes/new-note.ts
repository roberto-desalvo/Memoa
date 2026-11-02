import { Component, computed, DestroyRef, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink } from '@angular/router';
import { lastValueFrom, Observable, tap } from 'rxjs';
import { MAX_TEXT_LENGTH, NoteDetails, RecordsApi } from '../records/records-api';
import { RecordAudio } from '../recording/record-audio';
import { AudioUploadService } from '../uploads/audio-upload.service';
import { UploadFiles } from '../uploads/upload-files';
import { nowInputValue, recordingTitle, textTitle, toOccurredAt } from './note-date';

export type NewNoteMode = 'record' | 'write' | 'upload';

/** Contenuto acquisito nel primo passo. */
type Content = { kind: 'audio'; audio: Blob; contentType: string } | { kind: 'text'; text: string };

type Step = { kind: 'capture' } | { kind: 'saving'; fraction: number | null } | { kind: 'failed' };

/**
 * Creazione delle note. La scrittura ha titolo (proposta TXT_yyyyMMdd_HHmm dalla data della nota),
 * data e ora (di default adesso) e testo nella stessa pagina. La registrazione si salva subito, con data
 * e ora attuali e il titolo scritto sopra il registratore (proposta REC_yyyyMMdd_HHmm dall'avvio della
 * registrazione); titolo e data sono modificabili poi nella pagina del record. Gli upload (più file,
 * con anteprima di titolo e data) li gestisce UploadFiles. Per gli audio titolo e data vanno con la
 * richiesta del SAS di upload.
 */
@Component({
  selector: 'app-new-note',
  imports: [
    RouterLink,
    RecordAudio,
    UploadFiles,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatToolbarModule,
  ],
  templateUrl: './new-note.html',
  styleUrl: './new-note.scss',
  host: { class: 'page' },
})
export class NewNote {
  private readonly router = inject(Router);
  private readonly api = inject(RecordsApi);
  private readonly uploads = inject(AudioUploadService);
  private readonly destroyRef = inject(DestroyRef);

  /** Dal percorso /new/:mode. */
  readonly mode = input.required<NewNoteMode>();

  protected readonly step = signal<Step>({ kind: 'capture' });
  protected readonly content = signal<Content | null>(null);

  protected readonly text = signal('');
  /** Titolo del testo scritto dall'utente; null finché non lo modifica. */
  protected readonly title = signal<string | null>(null);
  protected readonly date = signal(nowInputValue());
  /** La proposta segue la data della nota, finché l'utente non scrive un titolo. */
  protected readonly titleProposal = computed(() =>
    textTitle(new Date(this.date() || nowInputValue())),
  );
  protected readonly canSaveText = computed(
    () => this.text().trim().length > 0 && this.text().length <= MAX_TEXT_LENGTH,
  );

  /** Titolo della registrazione scritto dall'utente; null finché non lo modifica. */
  protected readonly recordTitle = signal<string | null>(null);
  protected readonly recordTitleProposal = signal(recordingTitle(new Date()));

  protected readonly maxTextLength = MAX_TEXT_LENGTH;
  protected readonly heading = computed(
    () => ({ record: 'Registra', write: 'Scrivi', upload: 'Carica file' })[this.mode()],
  );

  // Dati dell'ultimo tentativo, per riprovare senza ripetere i passi.
  private note?: NoteDetails;
  private destroyed = false;

  constructor() {
    this.destroyRef.onDestroy(() => (this.destroyed = true));
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

  protected saveText(): void {
    if (!this.canSaveText()) {
      return;
    }
    const occurredAt = toOccurredAt(this.date() || nowInputValue());
    this.content.set({ kind: 'text', text: this.text() });
    this.note = {
      title: this.title()?.trim() || textTitle(new Date(occurredAt)),
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
