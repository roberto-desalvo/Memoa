import {
  Component,
  ElementRef,
  inject,
  input,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { inputValueOf, NoteDatePipe, toOccurredAt } from '../notes/note-date';
import {
  MAX_TEXT_LENGTH,
  ProcessingStep,
  RecordChanges,
  RecordDetail,
  RecordsApi,
} from './records-api';
import { POLL_INTERVAL_MS, RecordsStore } from './records-store';

/** Passi dell'elaborazione mostrati nella pagina, nell'ordine in cui avvengono. */
const AUDIO_STEPS: readonly ProcessingStep[] = ['Preparing', 'Transcribing', 'Indexing'];
const TEXT_STEPS: readonly ProcessingStep[] = ['Indexing'];

/**
 * Pagina di un record: audio normalizzato riproducibile, testo, modifica ed eliminazione.
 * L'audio si riproduce tramite un SAS di sola lettura chiesto alle Functions, appena esiste la copia
 * normalizzata. Un record in elaborazione mostra l'avanzamento e si aggiorna con il polling finché la
 * pagina è aperta e visibile; modifica e rigenerazione sono disponibili solo a elaborazione conclusa.
 */
@Component({
  selector: 'app-record-page',
  imports: [RouterLink, NoteDatePipe],
  templateUrl: './record-page.html',
  styleUrl: './record-page.scss',
  host: { class: 'page' },
})
export class RecordPage implements OnInit, OnDestroy {
  private readonly api = inject(RecordsApi);
  private readonly store = inject(RecordsStore);
  private readonly router = inject(Router);

  /** Dal percorso /records/:recordUid. */
  readonly recordUid = input.required<string>();

  protected readonly record = signal<RecordDetail | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly audioUrl = signal<string | null>(null);

  protected readonly editing = signal(false);
  protected readonly editTitle = signal('');
  protected readonly editDate = signal('');
  protected readonly editText = signal('');
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly maxTextLength = MAX_TEXT_LENGTH;

  private readonly deleteDialog = viewChild.required<ElementRef<HTMLDialogElement>>('deleteDialog');
  private readonly regenerateDialog =
    viewChild.required<ElementRef<HTMLDialogElement>>('regenerateDialog');

  private destroyed = false;
  private timer?: ReturnType<typeof setTimeout>;

  private readonly onVisibilityChange = () => {
    if (document.visibilityState === 'visible') {
      // Tornando sull'app l'elaborazione può essere avanzata nel frattempo.
      if (this.record()?.status === 'Processing') {
        void this.poll();
      }
    } else {
      clearTimeout(this.timer);
    }
  };

  async ngOnInit(): Promise<void> {
    document.addEventListener('visibilitychange', this.onVisibilityChange);
    try {
      await this.load();
    } catch (err) {
      console.error('Caricamento del record non riuscito', err);
      this.loadError.set(
        err instanceof HttpErrorResponse && err.status === 404
          ? 'Nota non trovata.'
          : 'Impossibile caricare la nota.',
      );
    }
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    clearTimeout(this.timer);
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
  }

  /** Passi da mostrare per il record e posizione di quello in corso. */
  protected stepsOf(
    record: RecordDetail,
  ): { step: ProcessingStep; state: 'done' | 'current' | 'pending' }[] {
    const steps = record.sourceType === 'Audio' ? AUDIO_STEPS : TEXT_STEPS;
    const current = steps.indexOf(record.processingStep ?? 'Indexing');
    return steps.map((step, i) => ({
      step,
      state: i < current ? 'done' : i === current ? 'current' : 'pending',
    }));
  }

  private async load(): Promise<void> {
    const record = await firstValueFrom(this.api.get(this.recordUid()));
    if (this.destroyed) {
      return;
    }
    this.show(record);
    // L'audio si può ascoltare già durante la trascrizione.
    if (record.hasAudio && !this.audioUrl()) {
      const { url } = await firstValueFrom(this.api.audio(record.recordUid));
      this.audioUrl.set(url);
    }
  }

  /** Mostra il record aggiornato e, se è ancora in elaborazione, pianifica il controllo successivo. */
  private show(record: RecordDetail): void {
    const previous = this.record();
    this.record.set(record);
    if (previous && previous.status !== record.status) {
      // La lista in memoria resta coerente senza attendere il suo polling.
      this.store.replace(record);
    }
    this.schedule();
  }

  private schedule(): void {
    clearTimeout(this.timer);
    if (
      !this.destroyed &&
      !this.editing() &&
      document.visibilityState === 'visible' &&
      this.record()?.status === 'Processing'
    ) {
      this.timer = setTimeout(() => void this.poll(), POLL_INTERVAL_MS);
    }
  }

  private async poll(): Promise<void> {
    clearTimeout(this.timer);
    try {
      await this.load();
    } catch (err) {
      // Un errore durante il polling lascia la pagina com'è; il tentativo successivo riprova.
      console.error('Aggiornamento del record non riuscito', err);
      this.schedule();
    }
  }

  protected startEdit(record: RecordDetail): void {
    this.editTitle.set(record.title);
    this.editDate.set(inputValueOf(record.occurredAt));
    this.editText.set(record.text ?? '');
    this.actionError.set(null);
    this.editing.set(true);
  }

  protected async saveEdit(record: RecordDetail): Promise<void> {
    const title = this.editTitle().trim();
    if (!title || !this.editDate()) {
      this.actionError.set('Titolo, data e ora sono obbligatori.');
      return;
    }

    const changes: RecordChanges = {};
    if (title !== record.title) {
      changes.title = title;
    }
    if (this.editDate() !== inputValueOf(record.occurredAt)) {
      // Cambiano data e ora al minuto: secondi e millisecondi restano quelli della nota.
      changes.occurredAt = toOccurredAt(this.editDate(), new Date(record.occurredAt));
    }
    const textChanged = record.text !== null && this.editText() !== record.text;
    if (textChanged) {
      if (!this.editText().trim()) {
        this.actionError.set('Il testo non può essere vuoto.');
        return;
      }
      changes.text = this.editText();
    }

    if (Object.keys(changes).length === 0) {
      this.editing.set(false);
      return;
    }

    this.busy.set(true);
    this.actionError.set(null);
    try {
      const updated = await firstValueFrom(this.api.update(record.recordUid, changes));
      this.store.replace(updated);
      // Se il testo è cambiato va indicizzato di nuovo: il record torna "in elaborazione" e la pagina
      // ne mostra l'avanzamento.
      this.editing.set(false);
      this.show(updated);
    } catch (err) {
      console.error('Modifica del record non riuscita', err);
      this.actionError.set(
        err instanceof HttpErrorResponse && err.status === 409
          ? 'La nota è stata aggiornata nel frattempo: ricarica la pagina e riprova.'
          : 'Salvataggio non riuscito.',
      );
    } finally {
      this.busy.set(false);
    }
  }

  protected confirmRegenerate(): void {
    this.actionError.set(null);
    this.regenerateDialog().nativeElement.showModal();
  }

  protected async regenerateTranscript(record: RecordDetail): Promise<void> {
    this.busy.set(true);
    try {
      const updated = await firstValueFrom(this.api.regenerateTranscript(record.recordUid));
      this.store.replace(updated);
      this.regenerateDialog().nativeElement.close();
      // Il record torna "in elaborazione" fino alla nuova trascrizione: la pagina ne mostra l'avanzamento.
      this.show(updated);
    } catch (err) {
      console.error('Rigenerazione della trascrizione non riuscita', err);
      this.actionError.set(
        err instanceof HttpErrorResponse && err.status === 409
          ? 'La nota è stata aggiornata nel frattempo: ricarica la pagina e riprova.'
          : 'Rigenerazione della trascrizione non riuscita.',
      );
      this.regenerateDialog().nativeElement.close();
    } finally {
      this.busy.set(false);
    }
  }

  protected confirmDelete(): void {
    this.actionError.set(null);
    this.deleteDialog().nativeElement.showModal();
  }

  protected async delete(record: RecordDetail): Promise<void> {
    this.busy.set(true);
    try {
      await firstValueFrom(this.api.delete(record.recordUid));
      this.store.remove(record.recordUid);
      this.deleteDialog().nativeElement.close();
      void this.router.navigate(['/']);
    } catch (err) {
      console.error('Eliminazione del record non riuscita', err);
      this.actionError.set('Eliminazione non riuscita.');
      this.deleteDialog().nativeElement.close();
    } finally {
      this.busy.set(false);
    }
  }
}
