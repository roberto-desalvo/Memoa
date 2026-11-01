import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NoteDatePipe } from '../notes/note-date';
import { NoteCard } from '../shared/note-card';
import { MAX_QUERY_LENGTH, SearchApi, SearchResult } from './search-api';

/**
 * Ricerca semantica in homepage: i frammenti delle note più simili alla richiesta (al massimo 10, sopra
 * la soglia di similarità decisa dalle Functions), ciascuno con il collegamento alla nota. Mentre ci
 * sono risultati (o una ricerca in corso) la homepage nasconde la lista; svuotando il campo torna.
 */
@Component({
  selector: 'app-note-search',
  imports: [
    RouterLink,
    NoteDatePipe,
    NoteCard,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './note-search.html',
  styleUrl: './note-search.scss',
})
export class NoteSearch {
  private readonly api = inject(SearchApi);

  protected readonly maxQueryLength = MAX_QUERY_LENGTH;
  protected readonly query = signal('');
  /** Null finché non si è cercato nulla. */
  protected readonly results = signal<SearchResult[] | null>(null);
  protected readonly status = signal<'idle' | 'searching' | 'failed'>('idle');

  /** La ricerca occupa la pagina al posto della lista delle note. */
  readonly active = computed(() => this.status() !== 'idle' || this.results() !== null);

  /** Ultima richiesta inviata: le risposte di richieste superate (o annullate) si ignorano. */
  private requestId = 0;

  protected onInput(value: string): void {
    this.query.set(value);
    if (!value.trim()) {
      this.clear();
    }
  }

  protected clear(): void {
    this.requestId++;
    this.query.set('');
    this.results.set(null);
    this.status.set('idle');
  }

  protected async search(): Promise<void> {
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
