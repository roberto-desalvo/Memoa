import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LIST_POLL_INTERVAL_MS } from '../config/polling';
import { EXCERPT_LENGTH, RecordDetail, RecordsApi, RecordSummary } from './records-api';

/**
 * Lista dei record della homepage. Il polling resta attivo solo mentre la homepage è aperta, l'app è
 * visibile e almeno un record è in elaborazione. Gli aggiornamenti non svuotano mai la lista e la
 * sostituiscono solo se è cambiata: con il `track` sul RecordUid gli elementi restano gli stessi nel DOM.
 * La lista resta in memoria tra una pagina e l'altra, così tornando alla homepage appare subito.
 */
@Injectable({ providedIn: 'root' })
export class RecordsStore {
  private readonly api = inject(RecordsApi);

  /** Null finché non arriva la prima risposta. */
  readonly records = signal<RecordSummary[] | null>(null);
  readonly loadFailed = signal(false);

  private active = false;
  private loading = false;
  private timer?: ReturnType<typeof setTimeout>;

  private readonly onVisibilityChange = () => {
    if (document.visibilityState === 'visible') {
      // Tornando sull'app lo stato può essere cambiato nel frattempo.
      if (this.hasProcessing()) {
        void this.refresh();
      }
    } else {
      clearTimeout(this.timer);
    }
  };

  /** Da chiamare all'apertura della homepage: aggiorna subito e avvia il polling se serve. */
  start(): void {
    if (this.active) {
      return;
    }
    this.active = true;
    document.addEventListener('visibilitychange', this.onVisibilityChange);
    void this.refresh();
  }

  stop(): void {
    this.active = false;
    clearTimeout(this.timer);
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
  }

  async refresh(): Promise<void> {
    clearTimeout(this.timer);
    if (this.loading) {
      return;
    }

    this.loading = true;
    try {
      const records = await firstValueFrom(this.api.list());
      if (!sameRecords(this.records(), records)) {
        this.records.set(records);
      }
      this.loadFailed.set(false);
    } catch (err) {
      // Un errore durante il polling lascia la lista com'è; il tentativo successivo riprova.
      console.error('Caricamento della lista non riuscito', err);
      this.loadFailed.set(true);
    } finally {
      this.loading = false;
      this.schedule();
    }
  }

  /** Aggiorna subito un record dopo un'azione dell'utente, senza attendere il polling. */
  replace(record: RecordSummary | RecordDetail): void {
    this.records.update(
      (records) =>
        records?.map((r) => (r.recordUid === record.recordUid ? toSummary(record) : r)) ?? null,
    );
    this.schedule();
  }

  remove(recordUid: string): void {
    this.records.update((records) => records?.filter((r) => r.recordUid !== recordUid) ?? null);
  }

  private schedule(): void {
    clearTimeout(this.timer);
    if (this.active && document.visibilityState === 'visible' && this.hasProcessing()) {
      this.timer = setTimeout(() => void this.refresh(), LIST_POLL_INTERVAL_MS);
    }
  }

  private hasProcessing(): boolean {
    return this.records()?.some((r) => r.status === 'Processing') ?? false;
  }
}

/** Voce della lista; da un record completo l'estratto si ricava dal testo, come fanno le Functions. */
function toSummary(record: RecordSummary | RecordDetail): RecordSummary {
  const { recordUid, title, occurredAt, sourceType, status, canRetry } = record;
  const excerpt =
    'text' in record ? (record.text?.slice(0, EXCERPT_LENGTH) ?? null) : (record.excerpt ?? null);
  const durationSeconds = record.durationSeconds ?? null;
  const tags = record.tags ?? [];
  return {
    recordUid,
    title,
    occurredAt,
    sourceType,
    status,
    canRetry,
    excerpt,
    durationSeconds,
    tags,
  };
}

function sameRecords(current: RecordSummary[] | null, next: RecordSummary[]): boolean {
  return current !== null && JSON.stringify(current) === JSON.stringify(next);
}
