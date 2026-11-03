import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccountState } from '../account/account-state';

/**
 * La pagina di amministrazione si apre solo per gli amministratori; gli altri tornano alla homepage. Il
 * controllo vero lo fanno le Functions, che rispondono 403 agli altri utenti.
 */
export const adminGuard: CanActivateFn = async () => {
  const router = inject(Router);
  const account = inject(AccountState);
  try {
    const me = await account.ensureRegistered();
    return me.isAdmin || router.parseUrl('/');
  } catch {
    return router.parseUrl('/');
  }
};
