import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatListModule, MatSelectionListChange } from '@angular/material/list';
import { firstValueFrom } from 'rxjs';
import { SystemSettingsValues } from './admin-api';

export type SettingKey = keyof SystemSettingsValues;

/** Parametro che si può riportare al valore predefinito. */
export interface RestorableSetting {
  key: SettingKey;
  label: string;
  /** Valore predefinito già formattato, es. "0.3" o "attivo". */
  defaultValue: string;
}

/**
 * Scelta dei parametri da riportare al valore predefinito. Si chiude con le chiavi scelte (mai vuote),
 * oppure undefined se annullata: i valori vanno nel form e si salvano solo con "Salva".
 */
@Component({
  selector: 'app-restore-defaults-dialog',
  imports: [MatButtonModule, MatDialogModule, MatListModule],
  template: `
    <h2 mat-dialog-title>Ripristina predefiniti</h2>
    <mat-dialog-content>
      <p class="text-secondary muted">
        I valori scelti tornano a quelli della configurazione. Per confermare, salva.
      </p>
      <mat-selection-list
        aria-label="Parametri da ripristinare"
        (selectionChange)="onSelection($event)"
      >
        @for (setting of settings; track setting.key) {
          <mat-list-option togglePosition="before" [value]="setting.key">
            <span matListItemTitle>{{ setting.label }}</span>
            <span matListItemLine>Predefinito: {{ setting.defaultValue }}</span>
          </mat-list-option>
        }
      </mat-selection-list>
    </mat-dialog-content>
    <mat-dialog-actions>
      <button mat-button mat-dialog-close>Annulla</button>
      <button mat-flat-button [disabled]="selected().length === 0" [mat-dialog-close]="selected()">
        Ripristina
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    p {
      margin: 0 0 var(--space-2);
    }
  `,
})
export class RestoreDefaultsDialog {
  protected readonly settings = inject<RestorableSetting[]>(MAT_DIALOG_DATA);
  protected readonly selected = signal<SettingKey[]>([]);

  protected onSelection(event: MatSelectionListChange): void {
    this.selected.set(event.source.selectedOptions.selected.map((option) => option.value));
  }
}

export function openRestoreDefaultsDialog(
  dialog: MatDialog,
  settings: RestorableSetting[],
): Promise<SettingKey[] | undefined> {
  return firstValueFrom(
    dialog
      .open<RestoreDefaultsDialog, RestorableSetting[], SettingKey[]>(RestoreDefaultsDialog, {
        data: settings,
      })
      .afterClosed(),
  );
}
