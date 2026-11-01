import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';

export interface ConfirmDialogData {
  title: string;
  message: string;
  /** Azione principale (pulsante pieno, a destra). Assente nelle finestre solo informative. */
  confirm?: string;
  /** Azione secondaria facoltativa, a sinistra (es. "Elimina" nella finestra di un record in errore). */
  secondary?: string;
  /** Pulsante che chiude senza fare nulla; di default "Annulla". */
  cancel?: string;
}

/** Undefined se la finestra si chiude con "Annulla", con Esc o toccando fuori. */
export type ConfirmDialogResult = 'confirm' | 'secondary' | undefined;

/** Unica finestra di conferma dell'app: titolo, testo, "Annulla" a sinistra e conferma a destra. */
@Component({
  selector: 'app-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>{{ data.message }}</mat-dialog-content>
    <mat-dialog-actions>
      @if (data.secondary) {
        <button mat-button class="secondary" mat-dialog-close="secondary">
          {{ data.secondary }}
        </button>
      }
      <button mat-button mat-dialog-close>{{ data.cancel ?? 'Annulla' }}</button>
      @if (data.confirm) {
        <button mat-flat-button mat-dialog-close="confirm">{{ data.confirm }}</button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    .secondary {
      margin-inline-end: auto;
    }
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}

export function openConfirmDialog(
  dialog: MatDialog,
  data: ConfirmDialogData,
): Promise<ConfirmDialogResult> {
  return firstValueFrom(
    dialog
      .open<ConfirmDialog, ConfirmDialogData, ConfirmDialogResult>(ConfirmDialog, { data })
      .afterClosed(),
  );
}

/** Conferma dell'eliminazione di una nota, uguale nella homepage e nella pagina del record. */
export const DELETE_NOTE_DIALOG: ConfirmDialogData = {
  title: 'Eliminare la nota?',
  message:
    'I file eliminati restano recuperabili per 14 giorni, poi vengono cancellati definitivamente.',
  confirm: 'Elimina',
};

/** Conferma dell'eliminazione di più note insieme dalla homepage, con il numero nel titolo. */
export function deleteNotesDialog(count: number): ConfirmDialogData {
  return count === 1 ? DELETE_NOTE_DIALOG : { ...DELETE_NOTE_DIALOG, title: `Eliminare ${count} note?` };
}
