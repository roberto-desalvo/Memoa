import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { SwUpdate } from '@angular/service-worker';
import { MsalService } from '@azure/msal-angular';
import { filter, first, map, switchMap } from 'rxjs';
import { Theme } from './settings/theme';
import { BottomNav } from './shared/bottom-nav';

@Component({
  imports: [RouterOutlet, BottomNav],
  selector: 'app-root',
  template: `
    <router-outlet />
    @if (navigated()) {
      <app-bottom-nav />
    }
  `,
})
export class App implements OnInit {
  private readonly msal = inject(MsalService);
  /**
   * La barra di navigazione compare dalla prima pagina aperta: prima l'utente può non aver ancora fatto
   * l'accesso (MsalGuard lo manda al login senza completare la navigazione).
   */
  protected readonly navigated = toSignal(
    inject(Router).events.pipe(
      first((event) => event instanceof NavigationEnd),
      map(() => true),
    ),
    { initialValue: false },
  );
  private readonly swUpdate = inject(SwUpdate);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  // Applica subito la modalità chiara / scura scelta sul dispositivo.
  private readonly theme = inject(Theme);

  ngOnInit(): void {
    // Completa il login al ritorno dal redirect di Entra External ID e imposta l'account attivo.
    this.msal
      .handleRedirectObservable()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        const account = result?.account ?? this.msal.instance.getAllAccounts()[0];
        if (account && !this.msal.instance.getActiveAccount()) {
          this.msal.instance.setActiveAccount(account);
        }
      });

    this.watchForUpdates();
  }

  /**
   * Nuova versione scaricata dal service worker: la propone senza ricaricare da sola, per non
   * interrompere una registrazione o una nota in scrittura.
   */
  private watchForUpdates(): void {
    if (!this.swUpdate.isEnabled) {
      return;
    }
    this.swUpdate.versionUpdates
      .pipe(
        filter((event) => event.type === 'VERSION_READY'),
        switchMap(() =>
          this.snackBar
            .open('È disponibile una nuova versione.', 'Aggiorna', { duration: 0 })
            .onAction(),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => document.location.reload());

    // Cache del service worker non più coerente con il server: l'unica via è ricaricare.
    this.swUpdate.unrecoverable
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => document.location.reload());
  }
}
