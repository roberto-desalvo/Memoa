import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import { MsalService } from '@azure/msal-angular';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  template: '<router-outlet />',
})
export class App implements OnInit {
  private readonly msal = inject(MsalService);
  private readonly destroyRef = inject(DestroyRef);

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
  }
}
