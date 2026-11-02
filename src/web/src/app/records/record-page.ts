import {
  afterNextRender,
  afterRenderEffect,
  Component,
  computed,
  ElementRef,
  inject,
  Injector,
  input,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
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
import { PAGE_POLL_INTERVAL_MS } from '../config/polling';
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
import { openRecordTagsDialog } from '../tags/record-tags-dialog';

/**
 * Attesa della ricarica dopo che il record è diventato pronto: i tag automatici si assegnano subito dopo
 * l'indicizzazione, quindi arrivano qualche istante dopo lo stato "pronto".
 */
const TAGS_RELOAD_DELAY_MS = 5_000;

/** Passi dell'elaborazione mostrati nella pagina, nell'ordine in cui avvengono. */
const AUDIO_STEPS: readonly ProcessingStep[] = ['Preparing', 'Transcribing', 'Indexing'];
const TEXT_STEPS: readonly ProcessingStep[] = ['Indexing'];

/** Porzione del testo della nota: semplice, occorrenza trovata o occorrenza corrente. */
interface TextPart {
  text: string;
  mark: 'none' | 'match' | 'current';
}

interface TextRange {
  start: number;
  end: number;
}

/** Testo ridotto per la ricerca, con la posizione nel testo originale di ogni suo carattere. */
interface FoldedText {
  text: string;
  /** Per ogni carattere di `text`, l'indice del carattere da cui deriva nel testo originale. */
  origins: number[];
}

/**
 * Riduce il testo per una ricerca che ignora maiuscole, accenti (in qualunque verso: "perche",
 * "perché" e "perchè" si trovano a vicenda) e la differenza tra apostrofo dritto e tipografico.
 * Ogni carattere si riduce da solo, così le posizioni trovate si riportano sul testo originale.
 */
function foldText(text: string): FoldedText {
  let folded = '';
  const origins: number[] = [];
  let index = 0;
  for (const char of text) {
    // Prima le minuscole: alcune (come "İ") producono a loro volta un segno diacritico da togliere.
    const reduced = char
      .toLowerCase()
      .normalize('NFD')
      .replace(/\p{M}/gu, '')
      .replace(/[‘’ʼ]/g, "'");
    folded += reduced;
    for (let i = 0; i < reduced.length; i++) {
      origins.push(index);
    }
    index += char.length;
  }
  return { text: folded, origins };
}

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
    MatChipsModule,
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

  /** Ricerca nel testo della nota: mentre è aperta l'evidenziazione è la sua, non quella semantica. */
  protected readonly finding = signal(false);
  protected readonly findQuery = signal('');
  private readonly findIndex = signal(0);

  /** Occorrenze della ricerca nel testo, senza distinguere maiuscole e minuscole. */
  protected readonly findMatches = computed((): TextRange[] => {
    const query = this.findQuery();
    if (!this.finding() || !query) {
      return [];
    }
    const needle = foldText(query).text;
    const text = this.record()?.text ?? '';
    const { text: haystack, origins } = this.foldedText();
    const matches: TextRange[] = [];
    if (!needle) {
      return matches;
    }
    for (
      let at = haystack.indexOf(needle);
      at >= 0;
      at = haystack.indexOf(needle, at + needle.length)
    ) {
      // La fine è il carattere originale successivo all'ultimo trovato (o la fine del testo), anche se
      // l'occorrenza finisce a metà di un carattere che si è ridotto in più caratteri.
      let next = at + needle.length;
      while (next < origins.length && origins[next] === origins[next - 1]) {
        next++;
      }
      matches.push({
        start: origins[at],
        end: next < origins.length ? origins[next] : text.length,
      });
    }
    return matches;
  });

  /** Testo della nota ridotto per la ricerca: si ricalcola solo quando cambia il testo. */
  private readonly foldedText = computed(() => foldText(this.record()?.text ?? ''));

  /** Posizione dell'occorrenza corrente; il testo può cambiare con il polling, quindi resta nei limiti. */
  protected readonly findCurrent = computed(() =>
    Math.min(this.findIndex(), this.findMatches().length - 1),
  );

  /**
   * Testo diviso attorno alle parti da evidenziare: le occorrenze della ricerca nel testo, se è aperta,
   * altrimenti il frammento della ricerca semantica. I frammenti sono porzioni esatte del testo
   * indicizzato: se il testo è cambiato nel frattempo e il frammento non c'è più, nessuna evidenziazione.
   */
  protected readonly textParts = computed((): TextPart[] => {
    const text = this.record()?.text ?? '';
    let ranges: TextRange[] = [];
    let current = 0;
    if (this.finding()) {
      ranges = this.findMatches();
      current = this.findCurrent();
    } else {
      const highlight = this.highlight();
      const start = highlight ? text.indexOf(highlight) : -1;
      if (highlight && start >= 0) {
        ranges = [{ start, end: start + highlight.length }];
      }
    }

    const parts: TextPart[] = [];
    let last = 0;
    ranges.forEach(({ start, end }, i) => {
      if (start > last) {
        parts.push({ text: text.slice(last, start), mark: 'none' });
      }
      parts.push({ text: text.slice(start, end), mark: i === current ? 'current' : 'match' });
      last = end;
    });
    if (last < text.length) {
      parts.push({ text: text.slice(last), mark: 'none' });
    }
    return parts;
  });

  private readonly injector = inject(Injector);
  private readonly header = viewChild('header', { read: ElementRef<HTMLElement> });
  private readonly findInput = viewChild<ElementRef<HTMLInputElement>>('findInput');
  private readonly currentMatch = viewChild<ElementRef<HTMLElement>>('currentMatch');
  private scrolledToMatch = false;

  /**
   * Il frammento della ricerca semantica, alla prima comparsa, viene portato al centro dello schermo.
   * L'occorrenza corrente della ricerca nel testo ci viene portata a ogni cambio, ma solo se è fuori
   * dalla parte visibile (sotto l'intestazione, che resta fissa in alto).
   */
  private readonly scrollToMatch = afterRenderEffect(() => {
    const match = this.currentMatch()?.nativeElement;
    // Dipendenza esplicita: a ogni carattere digitato o cambio di occorrenza si ricontrolla.
    this.textParts();
    if (!match) {
      return;
    }
    if (this.finding()) {
      const rect = match.getBoundingClientRect();
      const top = this.header()?.nativeElement.getBoundingClientRect().bottom ?? 0;
      const bottom = window.visualViewport?.height ?? window.innerHeight;
      if (rect.top < top || rect.bottom > bottom) {
        match.scrollIntoView({ block: 'center' });
      }
    } else if (!this.scrolledToMatch) {
      this.scrolledToMatch = true;
      match.scrollIntoView({ block: 'center' });
    }
  });

  private destroyed = false;
  private timer?: ReturnType<typeof setTimeout>;
  private tagsTimer?: ReturnType<typeof setTimeout>;

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
    clearTimeout(this.tagsTimer);
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
    if (previous?.status === 'Processing' && record.status === 'Ready') {
      this.scheduleTagsReload();
    }
    this.schedule();
  }

  /** Una sola ricarica, per mostrare i tag assegnati in automatico dopo l'indicizzazione. */
  private scheduleTagsReload(): void {
    clearTimeout(this.tagsTimer);
    this.tagsTimer = setTimeout(async () => {
      try {
        const record = await firstValueFrom(this.api.get(this.recordUid()));
        if (!this.destroyed && !this.editing()) {
          this.record.set(record);
          this.store.replace(record);
        }
      } catch (err) {
        // I tag compariranno alla prossima apertura della pagina.
        console.error('Aggiornamento dei tag non riuscito', err);
      }
    }, TAGS_RELOAD_DELAY_MS);
  }

  private schedule(): void {
    clearTimeout(this.timer);
    if (
      !this.destroyed &&
      !this.editing() &&
      document.visibilityState === 'visible' &&
      this.record()?.status === 'Processing'
    ) {
      this.timer = setTimeout(() => void this.poll(), PAGE_POLL_INTERVAL_MS);
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

  protected openFind(): void {
    this.finding.set(true);
    afterNextRender(() => this.findInput()?.nativeElement.focus(), { injector: this.injector });
  }

  /** Chiudendo la ricerca torna l'eventuale evidenziazione della ricerca semantica. */
  protected closeFind(): void {
    this.finding.set(false);
    this.findQuery.set('');
    this.findIndex.set(0);
  }

  protected onFindInput(query: string): void {
    this.findQuery.set(query);
    this.findIndex.set(0);
  }

  /** Occorrenza successiva o precedente, ricominciando dall'altro capo. */
  protected moveFind(step: 1 | -1): void {
    const count = this.findMatches().length;
    if (count > 0) {
      this.findIndex.set((this.findCurrent() + step + count) % count);
    }
  }

  /** Invio: occorrenza successiva (con Maiusc la precedente); Esc chiude la ricerca. */
  protected onFindKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.moveFind(event.shiftKey ? -1 : 1);
    } else if (event.key === 'Escape') {
      this.closeFind();
    }
  }

  protected startEdit(record: RecordDetail): void {
    this.closeFind();
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

  /** Scelta dei tag della nota; la pagina e la lista mostrano subito quelli salvati. */
  protected async editTags(record: RecordDetail): Promise<void> {
    const [updated] = (await openRecordTagsDialog(this.dialog, [record])) ?? [];
    if (updated && !this.destroyed) {
      this.record.set(updated);
    }
  }

  /** Copia negli appunti il testo completo della nota, senza le evidenziazioni della ricerca. */
  protected async copyText(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.snackBar.open('Testo copiato.');
    } catch (err) {
      console.error('Copia del testo non riuscita', err);
      this.snackBar.open('Copia non riuscita.');
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
