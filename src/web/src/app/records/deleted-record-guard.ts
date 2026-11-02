import { Location } from '@angular/common';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { RecordsStore } from './records-store';

/**
 * Con "indietro" (o "avanti") la cronologia può riportare alla pagina di una nota eliminata in questa
 * sessione: la navigazione si annulla, il router riscrive quella voce con l'indirizzo corrente e si
 * prosegue alla voce precedente, come se la nota non fosse mai stata aperta.
 */
export const deletedRecordGuard: CanActivateFn = (route) => {
  const router = inject(Router);
  const store = inject(RecordsStore);
  const location = inject(Location);
  const recordUid = route.paramMap.get('recordUid');
  if (
    router.currentNavigation()?.trigger !== 'popstate' ||
    !recordUid ||
    !store.isDeleted(recordUid)
  ) {
    return true;
  }
  // Dopo l'annullamento, quando il router ha già riscritto la voce della nota.
  setTimeout(() => location.back());
  return false;
};
