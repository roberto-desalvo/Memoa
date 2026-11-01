import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RecordStatus, RecordTag, SourceType } from '../records/records-api';

/**
 * Riga di una nota, unica per la lista della homepage e per i risultati della ricerca: icona del tipo,
 * titolo e, sotto, lo stato (se la nota non è pronta) oppure il testo, troncato con i puntini dopo
 * `lines` righe, e gli eventuali tag; a destra data o ora e, per gli audio, la durata. Il collegamento o il pulsante sta
 * attorno (classe note-link), dentro una lista note-list.
 */
@Component({
  selector: 'app-note-row',
  imports: [MatIconModule],
  template: `
    <mat-icon
      class="type"
      aria-hidden="false"
      [svgIcon]="sourceType() === 'Audio' ? 'mic' : 'text'"
      [attr.aria-label]="sourceType() === 'Audio' ? 'Nota audio' : 'Nota di testo'"
    />
    <span class="body">
      <span class="title strong" [class.muted]="status() !== 'Ready'">{{ title() }}</span>
      @if (status() === 'Error') {
        <span class="status failed"><mat-icon svgIcon="error" />Errore</span>
      } @else if (status() === 'Processing') {
        <span class="status"><mat-icon svgIcon="pending" />In elaborazione</span>
      } @else if (text()) {
        <!-- Gli a capo del testo diventano spazi: contano solo le righe mostrate. -->
        <span class="text text-secondary muted" [style.--lines]="lines()">{{ text() }}</span>
      }
      @if (tags()?.length) {
        <span class="tags text-label muted">
          <mat-icon svgIcon="tag" aria-hidden="false" aria-label="Tag" />
          <span class="tag-names">{{ tagNames() }}</span>
        </span>
      }
    </span>
    @if (!hideSide()) {
      <span class="side text-secondary muted">
        <span>{{ detail() }}</span>
        @if (duration()) {
          <span [attr.aria-label]="'Durata ' + duration()">{{ duration() }}</span>
        }
      </span>
    }
  `,
  styles: `
    :host {
      display: flex;
      align-items: flex-start;
      gap: var(--space-4);
      min-height: 64px;
      padding: var(--space-3) var(--space-4);
    }

    .type {
      flex: none;
      width: 20px;
      height: 20px;
      margin-top: 2px;
      color: var(--mat-sys-on-surface-variant);
    }

    .body {
      display: flex;
      flex: 1;
      flex-direction: column;
      gap: var(--space-1);
      min-width: 0;
    }

    .title {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .text {
      display: -webkit-box;
      overflow: hidden;
      overflow-wrap: anywhere;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: var(--lines);
      line-clamp: var(--lines);
    }

    .tags {
      display: flex;
      align-items: center;
      gap: var(--space-1);
      min-width: 0;

      .mat-icon {
        flex: none;
        width: 14px;
        height: 14px;
      }
    }

    .tag-names {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .side {
      display: flex;
      flex: none;
      flex-direction: column;
      align-items: flex-end;
      gap: var(--space-1);
      font-variant-numeric: tabular-nums;
    }
  `,
})
export class NoteRow {
  readonly sourceType = input.required<SourceType>();
  readonly title = input.required<string>();
  readonly status = input<RecordStatus>('Ready');
  /** Ora, giorno e mese o data, a destra del titolo. */
  readonly detail = input.required<string>();
  /** Durata dell'audio ("2:48"), sotto la data; null se non c'è. */
  readonly duration = input<string | null>(null);
  /** Estratto della nota o frammento trovato dalla ricerca; null se la nota non ha ancora testo. */
  readonly text = input<string | null | undefined>(null);
  /** Righe di testo mostrate prima dei puntini. */
  readonly lines = input(1);
  /** Tag della nota, mostrati su una riga sotto il testo; assenti nei risultati della ricerca. */
  readonly tags = input<RecordTag[] | null | undefined>(null);
  /** Nasconde data e durata, per lasciare il posto a un'azione sopra la riga (il cestino in homepage). */
  readonly hideSide = input(false);

  protected readonly tagNames = computed(() => (this.tags() ?? []).map((t) => t.name).join(' · '));
}
