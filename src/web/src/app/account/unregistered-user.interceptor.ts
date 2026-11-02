import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AccountState } from './account-state';
import { USER_NOT_REGISTERED } from './me-api';

/**
 * Utente applicativo non registrato (403 `user_not_registered`): torna alla homepage, che chiama `/me`.
 * L'errore prosegue comunque verso chi ha fatto la chiamata.
 */
export const unregisteredUserInterceptor: HttpInterceptorFn = (req, next) => {
  const account = inject(AccountState);
  const router = inject(Router);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (
        err instanceof HttpErrorResponse &&
        err.status === 403 &&
        err.error?.error === USER_NOT_REGISTERED
      ) {
        account.reset();
        void router.navigateByUrl('/');
      }
      return throwError(() => err);
    }),
  );
};
