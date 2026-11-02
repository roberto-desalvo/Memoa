import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule, MatSelectionListChange } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { firstValueFrom } from 'rxjs';
import { RecordDetail, RecordsApi, RecordSummary } from '../records/records-api';
import { RecordsStore } from '../records/records-store';
import { MAX_TAG_NAME_LENGTH, TagsApi, tagSaveError, TagSummary } from './tags-api';

export interface RecordTagsDialogData {
  records: (RecordSummary | RecordDetail)[];
}

/**
 * Scelta dei tag di una o più note tra quelli esistenti, con la possibilità di crearne uno nuovo (solo con
 * il nome: la descrizione si aggiunge dalla pagina del tag). Con più note sono scelti i tag comuni a tutte:
 * quelli aggiunti vanno a tutte, quelli comuni tolti si tolgono da tutte, gli altri tag di ciascuna nota
 * restano. Un tag tolto qui non viene più assegnato in automatico a quella nota. La lista mostra subito le
 * note salvate; con un salvataggio non riuscito la modale resta aperta e Salva riprova solo le note
 * mancanti. Si chiude con i record aggiornati, oppure undefined se annullata.
 */
@Component({
  selector: 'app-record-tags-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ title }}</h2>
    <mat-dialog-content>
      @if (tags(); as tags) {
        @if (tags.length > 0) {
          <mat-selection-list [attr.aria-label]="title" (selectionChange)="onSelection($event)">
            @for (tag of tags; track tag.tagUid) {
              <mat-list-option
                togglePosition="before"
                [value]="tag.tagUid"
                [selected]="selected().has(tag.tagUid)"
              >
                {{ tag.name }}
              </mat-list-option>
            }
          </mat-selection-list>
        } @else {
          <p class="text-secondary muted">Non hai ancora tag: creane uno qui sotto.</p>
        }
        <form class="new-tag" (submit)="$event.preventDefault(); create()">
          <mat-form-field class="field" subscriptSizing="dynamic">
            <mat-label>Nuovo tag</mat-label>
            <input
              matInput
              type="text"
              [maxLength]="maxNameLength"
              [value]="newName()"
              (input)="newName.set($any($event.target).value)"
            />
          </mat-form-field>
          <button mat-button type="submit" [disabled]="busy() || !newName().trim()">
            Aggiungi
          </button>
        </form>
      } @else if (loadFailed()) {
        <p class="error" role="alert"><mat-icon svgIcon="error" />Impossibile caricare i tag.</p>
      } @else {
        <div class="loading" aria-live="polite">
          <mat-spinner diameter="32" aria-label="Caricamento dei tag" />
        </div>
      }
      @if (actionError(); as message) {
        <p class="error" role="alert"><mat-icon svgIcon="error" />{{ message }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button mat-button mat-dialog-close [disabled]="busy()">Annulla</button>
      <button mat-flat-button [disabled]="busy() || !tags()" (click)="save()">Salva</button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-selection-list {
      margin: 0 calc(-1 * var(--space-5));
    }

    .new-tag {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      margin-top: var(--space-3);
    }

    .field {
      flex: 1;
      min-width: 0;
    }

    .loading {
      display: flex;
      justify-content: center;
      padding: var(--space-5);
    }

    .error {
      margin-top: var(--space-3);
    }
  `,
})
export class RecordTagsDialog implements OnInit {
  private readonly data = inject<RecordTagsDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<RecordTagsDialog, RecordDetail[]>>(MatDialogRef);
  private readonly tagsApi = inject(TagsApi);
  private readonly recordsApi = inject(RecordsApi);
  private readonly store = inject(RecordsStore);

  protected readonly title =
    this.data.records.length === 1 ? 'Tag della nota' : `Tag di ${this.data.records.length} note`;

  /** Tag comuni a tutte le note all'apertura: quelli tolti dalla scelta si tolgono da ogni nota. */
  private readonly common = commonTags(this.data.records);

  /** Note ancora da salvare e note già salvate (dopo un salvataggio riuscito solo in parte). */
  private pending = this.data.records;
  private readonly saved: RecordDetail[] = [];

  protected readonly tags = signal<TagSummary[] | null>(null);
  protected readonly loadFailed = signal(false);
  protected readonly selected = signal<ReadonlySet<string>>(new Set(this.common));
  protected readonly newName = signal('');
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly maxNameLength = MAX_TAG_NAME_LENGTH;

  async ngOnInit(): Promise<void> {
    try {
      this.tags.set(await firstValueFrom(this.tagsApi.list()));
    } catch (err) {
      console.error('Caricamento dei tag non riuscito', err);
      this.loadFailed.set(true);
    }
  }

  protected onSelection(event: MatSelectionListChange): void {
    const next = new Set(this.selected());
    for (const option of event.options) {
      if (option.selected) {
        next.add(option.value);
      } else {
        next.delete(option.value);
      }
    }
    this.selected.set(next);
  }

  /** Crea il tag (o, se esiste già con lo stesso nome, lo seleziona) e lo aggiunge alla scelta. */
  protected async create(): Promise<void> {
    const name = this.newName().trim();
    if (!name) {
      return;
    }
    const existing = this.tags()?.find((t) => t.name.toLowerCase() === name.toLowerCase());
    if (existing) {
      this.selected.set(new Set(this.selected()).add(existing.tagUid));
      this.newName.set('');
      return;
    }

    this.busy.set(true);
    this.actionError.set(null);
    try {
      const created = await firstValueFrom(this.tagsApi.create({ name }));
      this.tags.update((tags) =>
        [...(tags ?? []), created].sort((a, b) => a.name.localeCompare(b.name)),
      );
      this.selected.set(new Set(this.selected()).add(created.tagUid));
      this.newName.set('');
    } catch (err) {
      console.error('Creazione del tag non riuscita', err);
      this.actionError.set(tagSaveError(err));
    } finally {
      this.busy.set(false);
    }
  }

  protected async save(): Promise<void> {
    this.busy.set(true);
    this.actionError.set(null);
    const selected = this.selected();
    const removed = [...this.common].filter((tagUid) => !selected.has(tagUid));
    const records = this.pending;
    const results = await Promise.allSettled(
      records.map((record) => {
        const tagUids = new Set((record.tags ?? []).map((t) => t.tagUid));
        removed.forEach((tagUid) => tagUids.delete(tagUid));
        selected.forEach((tagUid) => tagUids.add(tagUid));
        return firstValueFrom(this.recordsApi.setTags(record.recordUid, [...tagUids]));
      }),
    );
    this.pending = records.filter((_, i) => results[i].status === 'rejected');
    results.forEach((result) => {
      if (result.status === 'fulfilled') {
        this.saved.push(result.value);
        this.store.replace(result.value);
      } else {
        console.error('Salvataggio dei tag della nota non riuscito', result.reason);
      }
    });
    this.busy.set(false);

    if (this.pending.length === 0) {
      this.dialogRef.close(this.saved);
    } else if (this.pending.length === records.length) {
      this.actionError.set('Salvataggio non riuscito. Riprova.');
    } else {
      const failed = this.pending.length;
      this.actionError.set(
        `Non è stato possibile salvare ${failed === 1 ? '1 nota' : `${failed} note`} su ${this.data.records.length}. Riprova.`,
      );
    }
  }
}

/** Tag presenti in tutte le note. */
function commonTags(records: (RecordSummary | RecordDetail)[]): Set<string> {
  const [first, ...others] = records.map((r) => new Set((r.tags ?? []).map((t) => t.tagUid)));
  return new Set([...(first ?? [])].filter((tagUid) => others.every((tags) => tags.has(tagUid))));
}

export function openRecordTagsDialog(
  dialog: MatDialog,
  records: (RecordSummary | RecordDetail)[],
): Promise<RecordDetail[] | undefined> {
  return firstValueFrom(
    dialog
      .open<RecordTagsDialog, RecordTagsDialogData, RecordDetail[]>(RecordTagsDialog, {
        data: { records },
        width: '28rem',
        maxWidth: 'calc(100vw - 32px)',
      })
      .afterClosed(),
  );
}
