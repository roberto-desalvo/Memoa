import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MAX_QUERY_LENGTH } from './search-api';
import { SearchState } from './search-state';

/**
 * Campo "Cerca in Memoa" dell'intestazione della homepage: la ricerca parte con Invio, svuotando il
 * campo (o con la ×) torna la lista. Il contenuto proiettato (l'avatar del menu utente) sta in fondo
 * al campo, a destra. I risultati li mostra {@link NoteSearch}.
 */
@Component({
  selector: 'app-search-bar',
  imports: [MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <form role="search" (submit)="$event.preventDefault(); state.search()">
      <mat-form-field class="field" subscriptSizing="dynamic">
        <mat-icon matPrefix svgIcon="search" />
        <input
          matInput
          type="search"
          aria-label="Cerca in Memoa"
          placeholder="Cerca in Memoa"
          enterkeyhint="search"
          [maxLength]="maxQueryLength"
          [value]="state.query()"
          (input)="onInput($any($event.target).value)"
        />
        <span matSuffix class="suffix">
          @if (state.query()) {
            <button
              mat-icon-button
              type="button"
              aria-label="Cancella la ricerca"
              (click)="state.clear()"
            >
              <mat-icon svgIcon="close" />
            </button>
          }
          <ng-content />
        </span>
      </mat-form-field>
    </form>
  `,
  styles: `
    :host {
      display: block;
    }

    // Campo a pillola, come le barre di ricerca di Gmail e Keep.
    .field {
      display: block;
      width: 100%;
      --mat-form-field-filled-container-shape: var(--space-6);
    }

    .field .mat-icon[matPrefix] {
      color: var(--mat-sys-on-surface-variant);
    }

    .suffix {
      display: flex;
      align-items: center;
      padding-inline-end: var(--space-1);
    }
  `,
})
export class SearchBar {
  protected readonly state = inject(SearchState);
  protected readonly maxQueryLength = MAX_QUERY_LENGTH;

  protected onInput(value: string): void {
    this.state.query.set(value);
    if (!value.trim()) {
      this.state.clear();
    }
  }
}
