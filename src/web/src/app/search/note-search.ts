import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { NoteFilters } from '../home/note-filters';
import { NoteDatePipe } from '../notes/note-date';
import { RecordsStore } from '../records/records-store';
import { NoteRow } from '../shared/note-row';
import { SearchResult } from './search-api';
import { SearchState } from './search-state';

/**
 * Risultati della ricerca semantica in homepage (il campo è {@link SearchBar}): i frammenti delle note
 * più simili alla richiesta (al massimo 10, sopra la soglia di similarità decisa dalle Functions),
 * ciascuno con il collegamento alla nota. Mentre ci
 * sono risultati (o una ricerca in corso) la homepage nasconde la lista; svuotando il campo torna.
 * Lo stato sta in {@link SearchState}, così sopravvive all'apertura di una nota dai risultati.
 */
@Component({
  selector: 'app-note-search',
  imports: [
    RouterLink,
    NoteDatePipe,
    NoteRow,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './note-search.html',
  styleUrl: './note-search.scss',
})
export class NoteSearch {
  private readonly state = inject(SearchState);
  private readonly store = inject(RecordsStore);
  private readonly filters = inject(NoteFilters);

  protected readonly status = this.state.status;
  readonly active = this.state.active;
  protected readonly filtered = this.filters.active;

  /**
   * Risultati senza le note eliminate dopo la ricerca (ad esempio aprendole dai risultati) e senza le
   * note che non passano i filtri della homepage.
   */
  protected readonly results = computed(() => {
    const results = this.state.results();
    const records = this.store.records();
    if (!results || !records) {
      return results;
    }
    const uids = new Set(records.filter(this.filters.matches()).map((r) => r.recordUid));
    return results.filter((r) => uids.has(r.recordUid));
  });

  protected open(result: SearchResult): void {
    this.state.open(result);
  }

  protected search(): Promise<void> {
    return this.state.search();
  }
}
