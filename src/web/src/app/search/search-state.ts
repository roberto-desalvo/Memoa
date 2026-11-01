import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SearchApi, SearchResult } from './search-api';

/**
 * Stato della ricerca in homepage: richiesta e risultati restano in memoria tra una pagina e l'altra,
 * così aprendo una nota dai risultati e tornando indietro la ricerca è ancora lì. Non va nell'URL:
 * la richiesta è testo dell'utente.
 */
@Injectable({ providedIn: 'root' })
export class SearchState {
  private readonly api = inject(SearchApi);

  readonly query = signal('');
  /** Null finché non si è cercato nulla. */
  readonly results = signal<SearchResult[] | null>(null);
  readonly status = signal<'idle' | 'searching' | 'failed'>('idle');

  /** Frammento del risultato aperto, da evidenziare nella pagina della nota. */
  private highlight: Pick<SearchResult, 'recordUid' | 'content'> | null = null;

  /** La ricerca occupa la pagina al posto della lista delle note. */
  readonly active = computed(() => this.status() !== 'idle' || this.results() !== null);

  /** Ultima richiesta inviata: le risposte di richieste superate (o annullate) si ignorano. */
  private requestId = 0;

  clear(): void {
    this.requestId++;
    this.query.set('');
    this.results.set(null);
    this.status.set('idle');
    this.highlight = null;
  }

  /** Apertura di una nota dai risultati: la pagina della nota evidenzia il frammento trovato. */
  open(result: SearchResult): void {
    this.highlight = { recordUid: result.recordUid, content: result.content };
  }

  /**
   * Frammento da evidenziare nella nota, se la si apre dai risultati. Vale una volta sola: riaprendo la
   * nota dalla lista non si evidenzia nulla.
   */
  takeHighlight(recordUid: string): string | null {
    const highlight = this.highlight;
    this.highlight = null;
    return highlight?.recordUid === recordUid ? highlight.content : null;
  }

  async search(): Promise<void> {
    const query = this.query().trim();
    if (!query) {
      return;
    }

    const requestId = ++this.requestId;
    this.status.set('searching');
    try {
      const results = await firstValueFrom(this.api.search(query));
      if (requestId === this.requestId) {
        this.results.set(results);
        this.status.set('idle');
      }
    } catch (err) {
      if (requestId === this.requestId) {
        console.error('Ricerca non riuscita', err);
        this.status.set('failed');
      }
    }
  }
}
