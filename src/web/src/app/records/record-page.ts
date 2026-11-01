import {
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
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
import { RecordsStore } from './records-store';
import { SearchState } from '../search/search-state';
import { DELETE_NOTE_DIALOG, openConfirmDialog } from '../shared/confirm-dialog';

/**
 * Intervallo del polling di un record in elaborazione: breve, perché l'avanzamento dei passi sembri in
 * tempo reale. Il controllo successivo parte solo dopo la risposta, quindi le richieste non si sovrappongono.
 */
const POLL_INTERVAL_MS = 1_000;

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
  imports: [
    RouterLink,
    NoteDatePipe,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatToolbarModule,
  ],
  templateUrl: './record-page.html',
  styleUrl: './record-page.scss',
  host: { class: 'page' },
})
export class RecordPage implements OnInit, OnDestroy {
  private readonly api = inject(RecordsApi);
  private readonly store = inject(RecordsStore);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly search = inject(SearchState);

  /** Dal percorso /records/:recordUid. */
  readonly recordUid = input.required<string>();

  protected readonly record = signal<RecordDetail | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly audioUrl = signal<string | null>(null);
  /** L'URL dell'audio non è arrivato o il browser non è riuscito a leggere il file. */
  protected readonly audioFailed = signal(false);

  protected readonly editing = signal(false);
  protected readonly editTitle = signal('');
  protected readonly editDate = signal('');
  protected readonly editText = signal('');
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly maxTextLength = MAX_TEXT_LENGTH;

  /** Frammento trovato dalla ricerca semantica, se la nota è stata aperta dai risultati. */
  private readonly highlight = signal<string | null>(null);

  /**
   * Testo diviso attorno al frammento da evidenziare. I frammenti sono porzioni esatte del testo
   * indicizzato: se il testo è cambiato nel frattempo e il frammento non c'è più, nessuna evidenziazione.
   */
  protected readonly textParts = computed(() => {
    const text = this.record()?.text ?? '';
    const highlight = this.highlight();
    const start = highlight ? text.indexOf(highlight) : -1;
    if (!highlight || start < 0) {
      return { before: text, match: '', after: '' };
    }
    const end = start + highlight.length;
    return { before: text.slice(0, start), match: text.slice(start, end), after: text.slice(end) };
  });

  private readonly match = viewChild<ElementRef<HTMLElement>>('match');
  private scrolledToMatch = false;

  /** Alla prima comparsa il frammento evidenziato viene portato al centro dello schermo. */
  private readonly scrollToMatch = effect(() => {
    const match = this.match();
    if (match && !this.scrolledToMatch) {
      this.scrolledToMatch = true;
      match.nativeElement.scrollIntoView({ block: 'center' });
    }
  });

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
    this.highlight.set(this.search.takeHighlight(this.recordUid()));
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
      await this.loadAudio(record);
    }
  }

  /**
   * Chiede il SAS di lettura dell'audio. Nel frattempo la card del lettore è già visibile con il
   * caricamento; se la richiesta fallisce la card mostra l'errore e, se il record è in elaborazione, il
   * polling successivo riprova.
   */
  private async loadAudio(record: RecordDetail): Promise<void> {
    try {
      const { url } = await firstValueFrom(this.api.audio(record.recordUid));
      this.audioFailed.set(false);
      this.audioUrl.set(url);
    } catch (err) {
      console.error('Caricamento dell’audio non riuscito', err);
      this.audioFailed.set(true);
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
      if (textChanged) {
        // Il frammento si riferiva al testo precedente.
        this.highlight.set(null);
      }
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

  /** "Riprova" su un record in errore: torna "in elaborazione" e la pagina ne mostra l'avanzamento. */
  protected async retry(record: RecordDetail): Promise<void> {
    this.busy.set(true);
    try {
      const updated = await firstValueFrom(this.api.retry(record.recordUid));
      this.store.replace(updated);
      this.show(updated);
    } catch (err) {
      console.error('Nuovo tentativo non riuscito', err);
      this.snackBar.open('Operazione non riuscita. Riprova più tardi.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async confirmRegenerate(): Promise<void> {
    const record = this.record();
    if (!record) {
      return;
    }
    const result = await openConfirmDialog(this.dialog, {
      title: 'Rigenerare la trascrizione?',
      message:
        'L’audio viene trascritto di nuovo e la nuova trascrizione sostituisce il testo attuale.' +
        (record.isTranscriptEdited ? ' Le modifiche che hai fatto al testo andranno perse.' : ''),
      confirm: 'Rigenera',
    });
    if (result !== 'confirm') {
      return;
    }

    this.busy.set(true);
    try {
      const updated = await firstValueFrom(this.api.regenerateTranscript(record.recordUid));
      this.store.replace(updated);
      // Il record torna "in elaborazione" fino alla nuova trascrizione: la pagina ne mostra l'avanzamento.
      this.show(updated);
    } catch (err) {
      console.error('Rigenerazione della trascrizione non riuscita', err);
      this.snackBar.open(
        err instanceof HttpErrorResponse && err.status === 409
          ? 'La nota è stata aggiornata nel frattempo: ricarica la pagina e riprova.'
          : 'Rigenerazione della trascrizione non riuscita.',
      );
    } finally {
      this.busy.set(false);
    }
  }

  protected async confirmDelete(): Promise<void> {
    const record = this.record();
    if (!record || (await openConfirmDialog(this.dialog, DELETE_NOTE_DIALOG)) !== 'confirm') {
      return;
    }

    this.busy.set(true);
    try {
      await firstValueFrom(this.api.delete(record.recordUid));
      this.store.remove(record.recordUid);
      void this.router.navigate(['/']);
    } catch (err) {
      console.error('Eliminazione del record non riuscita', err);
      this.snackBar.open('Eliminazione non riuscita.');
    } finally {
      this.busy.set(false);
    }
  }
}
