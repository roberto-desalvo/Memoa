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

  /** La ricerca occupa la pagina al posto della lista delle note. */
  readonly active = computed(() => this.status() !== 'idle' || this.results() !== null);

  /** Ultima richiesta inviata: le risposte di richieste superate (o annullate) si ignorano. */
  private requestId = 0;

  clear(): void {
    this.requestId++;
    this.query.set('');
    this.results.set(null);
    this.status.set('idle');
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
