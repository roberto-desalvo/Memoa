import { NgTemplateOutlet } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AdminApi, SystemSettings, SystemSettingsValues } from './admin-api';

type NumericSetting = Exclude<keyof SystemSettingsValues, 'speechEnabled'>;

/** Le similarità sono decimali, gli altri valori interi (come nelle Functions). */
const DECIMAL_SETTINGS: readonly NumericSetting[] = ['searchMinSimilarity', 'taggingMinSimilarity'];

/** Campo numerico della pagina, con l'intervallo ammesso dalle Functions. */
interface NumericField {
  key: NumericSetting;
  label: string;
  min: number;
  max: number;
  step: number;
}

/**
 * Parametri di sistema, solo per gli amministratori ({@link adminGuard}): trascrizione con Azure AI
 * Speech, soglie di similarità di ricerca e tag e altri valori da tarare. Un campo vuoto usa il valore
 * della configurazione delle Functions, mostrato come predefinito. Le altre istanze delle Functions
 * applicano le modifiche entro un minuto.
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

  protected readonly defaults = signal<SystemSettingsValues | null>(null);
  protected readonly loadFailed = signal(false);
  protected readonly status = signal<'idle' | 'saving' | 'saved' | 'failed'>('idle');
  protected readonly saveError = signal<string | null>(null);

  /** 'default' usa la configurazione. */
  protected readonly speech = signal<'default' | 'on' | 'off'>('default');
  /** Testo dei campi numerici; vuoto usa la configurazione. */
  protected readonly numbers = signal<Record<NumericSetting, string>>(emptyNumbers());

  protected readonly speechFields: NumericField[] = [
    {
      key: 'speechBaseTimeoutSeconds',
      label: 'Timeout di base (secondi)',
      min: 1,
      max: 600,
      step: 1,
    },
    {
      key: 'speechTimeoutPerAudioMinuteSeconds',
      label: 'Timeout per minuto di audio (secondi)',
      min: 0,
      max: 120,
      step: 1,
    },
  ];

  protected readonly searchFields: NumericField[] = [
    { key: 'searchMinSimilarity', label: 'Similarità minima (0–1)', min: 0, max: 1, step: 0.01 },
    { key: 'searchMaxResults', label: 'Numero massimo di risultati', min: 1, max: 50, step: 1 },
  ];

  protected readonly taggingFields: NumericField[] = [
    { key: 'taggingMinSimilarity', label: 'Similarità minima (0–1)', min: 0, max: 1, step: 0.01 },
    {
      key: 'taggingPendingTimeoutMinutes',
      label: 'Attesa massima dell’assegnazione (minuti)',
      min: 1,
      max: 1440,
      step: 1,
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
    return this.numbers()[key];
  }

  protected setNumber(key: NumericSetting, value: string): void {
    this.numbers.update((numbers) => ({ ...numbers, [key]: value }));
    this.status.set('idle');
  }

  protected setSpeech(value: 'default' | 'on' | 'off'): void {
    this.speech.set(value);
    this.status.set('idle');
  }

  /** Il campo torna vuoto: si usa il valore della configurazione. */
  protected clear(key: NumericSetting): void {
    this.setNumber(key, '');
  }

  protected async save(): Promise<void> {
    const values = this.values();
    if (!values) {
      this.saveError.set('Controlla i valori: alcuni non sono numeri validi.');
      this.status.set('failed');
      return;
    }
    this.status.set('saving');
    this.saveError.set(null);
    try {
      this.apply(await firstValueFrom(this.api.update(values)));
      this.status.set('saved');
    } catch (err) {
      console.error('Salvataggio dei parametri di sistema non riuscito', err);
      this.saveError.set(
        err instanceof HttpErrorResponse && err.error?.error === 'invalid_setting'
          ? 'Un valore è fuori dall’intervallo ammesso.'
          : 'Impossibile salvare i parametri.',
      );
      this.status.set('failed');
    }
  }

  private apply(settings: SystemSettings): void {
    this.defaults.set(settings.defaults);
    const values = settings.values;
    this.speech.set(
      values.speechEnabled === null ? 'default' : values.speechEnabled ? 'on' : 'off',
    );
    const numbers = emptyNumbers();
    for (const key of Object.keys(numbers) as NumericSetting[]) {
      numbers[key] = values[key]?.toString() ?? '';
    }
    this.numbers.set(numbers);
  }

  /** Valori da salvare, oppure null se un campo non è un numero (o non è intero dove serve). */
  private values(): SystemSettingsValues | null {
    const speech = this.speech();
    const values: SystemSettingsValues = {
      speechEnabled: speech === 'default' ? null : speech === 'on',
      ...emptyValues(),
    };
    const numbers = this.numbers();
    for (const key of Object.keys(numbers) as NumericSetting[]) {
      // Anche la virgola come separatore decimale.
      const text = numbers[key].trim().replace(',', '.');
      if (text === '') {
        continue;
      }
      const value = Number(text);
      if (
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

function emptyNumbers(): Record<NumericSetting, string> {
  return {
    speechBaseTimeoutSeconds: '',
    speechTimeoutPerAudioMinuteSeconds: '',
    searchMinSimilarity: '',
    searchMaxResults: '',
    taggingMinSimilarity: '',
    taggingPendingTimeoutMinutes: '',
  };
}

function emptyValues(): Omit<SystemSettingsValues, 'speechEnabled'> {
  return {
    speechBaseTimeoutSeconds: null,
    speechTimeoutPerAudioMinuteSeconds: null,
    searchMinSimilarity: null,
    searchMaxResults: null,
    taggingMinSimilarity: null,
    taggingPendingTimeoutMinutes: null,
  };
}
