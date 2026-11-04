import { NgTemplateOutlet } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AdminApi, SystemSettings, SystemSettingsValues } from './admin-api';
import {
  openRestoreDefaultsDialog,
  RestorableSetting,
  SettingKey,
} from './restore-defaults-dialog';

type NumericSetting = Exclude<SettingKey, 'speechEnabled'>;

/** Le similarità sono decimali, gli altri valori interi (come nelle Functions). */
const DECIMAL_SETTINGS: readonly NumericSetting[] = ['searchMinSimilarity', 'taggingMinSimilarity'];

/** Campo numerico della pagina, con l'intervallo ammesso dalle Functions. */
interface NumericField {
  key: NumericSetting;
  label: string;
  min: number;
  max: number;
}

/**
 * Valore di un campo: sempre quello in uso, mostrato nel campo. Con `isDefault` il valore è quello della
 * configurazione e si salva come null, così segue eventuali modifiche della configurazione.
 */
interface FieldState<T> {
  value: T;
  isDefault: boolean;
}

const SPEECH_LABEL = 'Azure AI Speech';

/**
 * Parametri di sistema, solo per gli amministratori ({@link adminGuard}): trascrizione con Azure AI
 * Speech, soglie di similarità di ricerca e tag e altri valori da tarare. Ogni campo mostra il valore in
 * uso; "Ripristina predefiniti" riporta i parametri scelti al valore della configurazione delle Functions,
 * da confermare salvando. Le altre istanze delle Functions applicano le modifiche entro un minuto.
 */
@Component({
  selector: 'app-admin-page',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatToolbarModule,
  ],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.scss',
  host: { class: 'page' },
})
export class AdminPage implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly defaults = signal<SystemSettingsValues | null>(null);
  protected readonly loadFailed = signal(false);
  protected readonly saving = signal(false);

  protected readonly speech = signal<FieldState<boolean>>({ value: true, isDefault: true });
  /** Testo dei campi numerici, come scritto dall'utente. */
  protected readonly numbers = signal<Record<NumericSetting, FieldState<string>> | null>(null);

  protected readonly speechFields: NumericField[] = [
    { key: 'speechBaseTimeoutSeconds', label: 'Timeout di base (secondi)', min: 1, max: 600 },
    {
      key: 'speechTimeoutPerAudioMinuteSeconds',
      label: 'Timeout per minuto di audio (secondi)',
      min: 0,
      max: 120,
    },
  ];

  protected readonly searchFields: NumericField[] = [
    { key: 'searchMinSimilarity', label: 'Similarità minima', min: 0, max: 1 },
    { key: 'searchMaxResults', label: 'Numero massimo di risultati', min: 1, max: 50 },
  ];

  protected readonly taggingFields: NumericField[] = [
    { key: 'taggingMinSimilarity', label: 'Similarità minima', min: 0, max: 1 },
    {
      key: 'taggingPendingTimeoutMinutes',
      label: 'Attesa massima dell’assegnazione (minuti)',
      min: 1,
      max: 1440,
    },
  ];

  async ngOnInit(): Promise<void> {
    try {
      this.apply(await firstValueFrom(this.api.get()));
    } catch (err) {
      console.error('Caricamento dei parametri di sistema non riuscito', err);
      this.loadFailed.set(true);
    }
  }

  protected defaultOf(key: NumericSetting): number | null {
    return this.defaults()?.[key] ?? null;
  }

  protected numberOf(key: NumericSetting): string {
    return this.numbers()?.[key].value ?? '';
  }

  protected setNumber(key: NumericSetting, value: string): void {
    this.numbers.update((numbers) => numbers && { ...numbers, [key]: { value, isDefault: false } });
  }

  protected setSpeech(value: boolean): void {
    this.speech.set({ value, isDefault: false });
  }

  /** Parametri non già al valore predefinito: si sceglie quali riportare, poi si salva. */
  protected async restoreDefaults(): Promise<void> {
    const keys = await openRestoreDefaultsDialog(this.dialog, this.restorable());
    const defaults = this.defaults();
    if (!keys || !defaults) {
      return;
    }
    for (const key of keys) {
      if (key === 'speechEnabled') {
        this.speech.set({ value: defaults.speechEnabled ?? true, isDefault: true });
      } else {
        this.numbers.update(
          (numbers) =>
            numbers && { ...numbers, [key]: { value: format(defaults[key]), isDefault: true } },
        );
      }
    }
  }

  protected canRestore(): boolean {
    return this.restorable().length > 0;
  }

  protected async save(): Promise<void> {
    const values = this.values();
    if (!values) {
      this.snackBar.open('Controlla i valori: alcuni non sono numeri validi.');
      return;
    }
    this.saving.set(true);
    try {
      this.apply(await firstValueFrom(this.api.update(values)));
      this.snackBar.open('Parametri salvati.');
    } catch (err) {
      console.error('Salvataggio dei parametri di sistema non riuscito', err);
      this.snackBar.open(
        err instanceof HttpErrorResponse && err.error?.error === 'invalid_setting'
          ? 'Un valore è fuori dall’intervallo ammesso.'
          : 'Salvataggio non riuscito.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  private restorable(): RestorableSetting[] {
    const defaults = this.defaults();
    const numbers = this.numbers();
    if (!defaults || !numbers) {
      return [];
    }
    const settings: RestorableSetting[] = [];
    if (!this.speech().isDefault) {
      settings.push({
        key: 'speechEnabled',
        label: SPEECH_LABEL,
        defaultValue: defaults.speechEnabled ? 'attivo' : 'disattivato',
      });
    }
    for (const field of [...this.speechFields, ...this.searchFields, ...this.taggingFields]) {
      if (!numbers[field.key].isDefault) {
        settings.push({
          key: field.key,
          label: field.label,
          defaultValue: format(defaults[field.key]),
        });
      }
    }
    return settings;
  }

  /** Ogni campo mostra il valore salvato o, se manca, quello della configurazione. */
  private apply(settings: SystemSettings): void {
    const { values, defaults } = settings;
    this.defaults.set(defaults);
    this.speech.set({
      value: values.speechEnabled ?? defaults.speechEnabled ?? true,
      isDefault: values.speechEnabled === null,
    });
    const numbers = {} as Record<NumericSetting, FieldState<string>>;
    for (const field of [...this.speechFields, ...this.searchFields, ...this.taggingFields]) {
      const stored = values[field.key];
      numbers[field.key] = {
        value: format(stored ?? defaults[field.key]),
        isDefault: stored === null,
      };
    }
    this.numbers.set(numbers);
  }

  /** Valori da salvare, oppure null se un campo non è un numero (o non è intero dove serve). */
  private values(): SystemSettingsValues | null {
    const speech = this.speech();
    const numbers = this.numbers();
    if (!numbers) {
      return null;
    }
    const values = {
      speechEnabled: speech.isDefault ? null : speech.value,
    } as SystemSettingsValues;
    for (const key of Object.keys(numbers) as NumericSetting[]) {
      const field = numbers[key];
      if (field.isDefault) {
        values[key] = null;
        continue;
      }
      // Anche la virgola come separatore decimale.
      const value = Number(field.value.trim().replace(',', '.'));
      if (
        field.value.trim() === '' ||
        !Number.isFinite(value) ||
        (!DECIMAL_SETTINGS.includes(key) && !Number.isInteger(value))
      ) {
        return null;
      }
      values[key] = value;
    }
    return values;
  }
}

function format(value: number | boolean | null): string {
  return value?.toString() ?? '';
}
