import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Me, MeApi } from './me-api';

/**
 * Registrazione dell'utente applicativo: la homepage chiama {@link ensureRegistered} all'apertura, le altre
 * pagine no. Se una chiamata risponde che l'utente non è registrato (per esempio dopo l'eliminazione
 * dell'account da un altro dispositivo), {@link unregisteredUserInterceptor} azzera lo stato e torna alla
 * homepage, che registra di nuovo.
 */
@Injectable({ providedIn: 'root' })
export class AccountState {
  private readonly api = inject(MeApi);

  /** Null finché l'utente non risulta registrato in questa sessione. */
  readonly me = signal<Me | null>(null);

  private pending: Promise<Me> | null = null;

  /** Una sola chiamata a `/me` anche con richieste concorrenti; poi la risposta resta in memoria. */
  ensureRegistered(): Promise<Me> {
    const me = this.me();
    if (me) {
      return Promise.resolve(me);
    }
    this.pending ??= firstValueFrom(this.api.get())
      .then((result) => {
        this.me.set(result);
        return result;
      })
      .finally(() => (this.pending = null));
    return this.pending;
  }

  reset(): void {
    this.me.set(null);
  }

  guideDismissed(): void {
    this.me.update((me) => me && { ...me, showGuide: false });
  }
}
