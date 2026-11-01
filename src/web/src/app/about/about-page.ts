import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { MsalService } from '@azure/msal-angular';
import { firstValueFrom } from 'rxjs';
import { openConfirmDialog } from '../shared/confirm-dialog';
import { AccountApi } from './account-api';

/** Informazioni sull'account ed eliminazione dell'account con tutti i dati. */
@Component({
  selector: 'app-about-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatIconModule,
    MatToolbarModule,
  ],
  templateUrl: './about-page.html',
  styleUrl: './about-page.scss',
  host: { class: 'page' },
})
export class AboutPage {
  private readonly msal = inject(MsalService);
  private readonly api = inject(AccountApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  private readonly account =
    this.msal.instance.getActiveAccount() ?? this.msal.instance.getAllAccounts()[0];

  protected readonly accountName = this.account?.name;
  /** Nei tenant esterni è l'email usata per accedere. */
  protected readonly username = this.account?.username;

  protected readonly deleting = signal(false);

  protected async confirmDelete(): Promise<void> {
    const result = await openConfirmDialog(this.dialog, {
      title: 'Eliminare l’account?',
      message:
        'Verranno eliminati tutti i tuoi dati: note, registrazioni audio, trascrizioni e impostazioni. ' +
        'L’operazione non si può annullare. I file eliminati restano nei sistemi di recupero per 14 ' +
        'giorni, poi vengono cancellati definitivamente.',
      confirm: 'Elimina account',
    });
    if (result !== 'confirm') {
      return;
    }

    this.deleting.set(true);
    try {
      await firstValueFrom(this.api.delete());
    } catch (err) {
      console.error('Eliminazione dell’account non riuscita', err);
      this.snackBar.open('Eliminazione dell’account non riuscita. Riprova.');
      this.deleting.set(false);
      return;
    }
    // I dati non esistono più: si esce, e MSAL cancella la sessione salvata nel browser.
    this.msal.logoutRedirect();
  }
}
