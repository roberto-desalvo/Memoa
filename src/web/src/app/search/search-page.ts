import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NoteDatePipe } from '../notes/note-date';
import { MAX_QUERY_LENGTH, SearchApi, SearchResult } from './search-api';

/**
 * Ricerca semantica: i frammenti delle note più simili alla richiesta (al massimo 10, sopra la soglia
 * di similarità decisa dalle Functions), ciascuno con il collegamento alla nota.
 */
@Component({
  selector: 'app-search-page',
  imports: [RouterLink, NoteDatePipe],
  templateUrl: './search-page.html',
  styleUrl: './search-page.scss',
  host: { class: 'page' },
})
export class SearchPage {
  private readonly api = inject(SearchApi);

  protected readonly maxQueryLength = MAX_QUERY_LENGTH;
  protected readonly query = signal('');
  /** Null finché non si è cercato nulla. */
  protected readonly results = signal<SearchResult[] | null>(null);
  protected readonly status = signal<'idle' | 'searching' | 'failed'>('idle');

  protected async search(): Promise<void> {
    const query = this.query().trim();
    if (!query || this.status() === 'searching') {
      return;
    }

    this.status.set('searching');
    try {
      this.results.set(await firstValueFrom(this.api.search(query)));
      this.status.set('idle');
    } catch (err) {
      console.error('Ricerca non riuscita', err);
      this.status.set('failed');
    }
  }
}
