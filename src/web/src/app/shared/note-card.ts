import { Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RecordStatus, SourceType } from '../records/records-api';

/**
 * Card di una nota, unica per la lista della homepage e per i risultati della ricerca: icona del tipo,
 * titolo con l'eventuale stato, poi una riga di dettaglio (ora o data) seguita dal testo, al massimo
 * due righe con i puntini di sospensione. Il collegamento o il pulsante sta attorno (classe card-link).
 */
@Component({
  selector: 'app-note-card',
  imports: [MatCardModule, MatIconModule],
  template: `
    <mat-card>
      <mat-card-content class="card" [class.dimmed]="status() !== 'Ready'">
        <mat-icon
          class="type"
          aria-hidden="false"
          [svgIcon]="sourceType() === 'Audio' ? 'audio' : 'text'"
          [attr.aria-label]="sourceType() === 'Audio' ? 'Nota audio' : 'Nota di testo'"
        />
        <span class="body">
          <span class="head">
            <span class="title strong">{{ title() }}</span>
            @if (status() === 'Error') {
              <span class="status failed"><mat-icon svgIcon="error" />Errore</span>
            } @else if (status() === 'Processing') {
              <span class="status"><mat-icon svgIcon="pending" />In elaborazione</span>
            }
          </span>
          <!-- Gli a capo del testo diventano spazi: contano solo le due righe mostrate. -->
          <span class="text text-secondary muted">
            {{ detail() }}
            @if (text()) {
              · {{ text() }}
            }
          </span>
        </span>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    :host {
      display: block;
    }

    .card {
      display: flex;
      align-items: flex-start;
      gap: var(--space-3);
      padding: var(--space-3) var(--space-4);
    }

    .type {
      flex: none;
      color: var(--mat-sys-on-surface-variant);
    }

    .body {
      display: flex;
      flex: 1;
      flex-direction: column;
      gap: var(--space-1);
      min-width: 0;
    }

    .head {
      display: flex;
      align-items: center;
      gap: var(--space-2);
    }

    .title {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .text {
      display: -webkit-box;
      overflow: hidden;
      overflow-wrap: anywhere;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 2;
      line-clamp: 2;
    }

    /* Nota non pronta: titolo attenuato, sempre accompagnato dall'etichetta di stato. */
    .dimmed .title {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class NoteCard {
  readonly sourceType = input.required<SourceType>();
  readonly title = input.required<string>();
  readonly status = input<RecordStatus>('Ready');
  /** Ora (nella lista raggruppata per giorno) o data (nei risultati della ricerca). */
  readonly detail = input.required<string>();
  /** Estratto della nota o frammento trovato dalla ricerca; null se la nota non ha ancora testo. */
  readonly text = input<string | null | undefined>(null);
}
