import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AccountState } from '../account/account-state';
import { Language, SettingsApi } from './settings-api';
import { Theme, ThemePreference } from './theme';

/**
 * Impostazioni: lingua preferita (salvata nell'account) e modalità chiara / scura (salvata nel
 * browser). Il nome visualizzato è rimandato. Agli amministratori mostra il collegamento ai parametri di
 * sistema.
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
  private readonly snackBar = inject(MatSnackBar);
  protected readonly theme = inject(Theme);
  protected readonly account = inject(AccountState);

  protected readonly language = signal<Language | null>(null);
  protected readonly saving = signal(false);
  protected readonly loadFailed = signal(false);

  protected readonly themes: { value: ThemePreference; label: string }[] = [
    { value: 'system', label: 'Come il sistema' },
    { value: 'light', label: 'Chiara' },
    { value: 'dark', label: 'Scura' },
  ];

  async ngOnInit(): Promise<void> {
    // Già in memoria se si arriva dalla homepage; serve solo a sapere se l'utente è amministratore.
    this.account
      .ensureRegistered()
      .catch((err) => console.error('Lettura dell’utente non riuscita', err));
    try {
      const settings = await firstValueFrom(this.api.get());
      this.language.set(settings.preferredLanguage);
    } catch (err) {
      console.error('Caricamento delle impostazioni non riuscito', err);
      this.loadFailed.set(true);
    }
  }

  protected async setLanguage(language: Language): Promise<void> {
    const previous = this.language();
    this.language.set(language);
    this.saving.set(true);
    try {
      await firstValueFrom(this.api.update({ preferredLanguage: language }));
      this.snackBar.open('Lingua salvata.');
    } catch (err) {
      console.error('Salvataggio della lingua non riuscito', err);
      this.language.set(previous);
      this.snackBar.open('Salvataggio della lingua non riuscito.');
    } finally {
      this.saving.set(false);
    }
  }
}
