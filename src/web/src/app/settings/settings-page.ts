import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Language, SettingsApi } from './settings-api';
import { Theme, ThemePreference } from './theme';

/**
 * Impostazioni: lingua preferita (salvata nell'account) e modalità chiara / scura (salvata nel
 * browser). Il nome visualizzato è rimandato.
 */
@Component({
  selector: 'app-settings-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    MatToolbarModule,
  ],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
  host: { class: 'page' },
})
export class SettingsPage implements OnInit {
  private readonly api = inject(SettingsApi);
  protected readonly theme = inject(Theme);

  protected readonly language = signal<Language | null>(null);
  protected readonly status = signal<'idle' | 'saving' | 'saved' | 'failed'>('idle');

  protected readonly themes: { value: ThemePreference; label: string }[] = [
    { value: 'system', label: 'Come il sistema' },
    { value: 'light', label: 'Chiara' },
    { value: 'dark', label: 'Scura' },
  ];

  async ngOnInit(): Promise<void> {
    try {
      const settings = await firstValueFrom(this.api.get());
      this.language.set(settings.preferredLanguage);
    } catch (err) {
      console.error('Caricamento delle impostazioni non riuscito', err);
      this.status.set('failed');
    }
  }

  protected async setLanguage(language: Language): Promise<void> {
    const previous = this.language();
    this.language.set(language);
    this.status.set('saving');
    try {
      await firstValueFrom(this.api.update({ preferredLanguage: language }));
      this.status.set('saved');
    } catch (err) {
      console.error('Salvataggio della lingua non riuscito', err);
      this.language.set(previous);
      this.status.set('failed');
    }
  }
}
