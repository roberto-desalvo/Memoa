import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom, Observable } from 'rxjs';

/**
 * Elimina più voci in parallelo (selezione multipla o cestino della singola voce) e restituisce quelle
 * eliminate. Se qualcuna non riesce un messaggio dice quante, con `countLabel` ("2 note", "2 tag").
 */
export async function deleteItems<T>(
  items: readonly T[],
  remove: (item: T) => Observable<unknown>,
  snackBar: MatSnackBar,
  countLabel: (count: number) => string,
): Promise<T[]> {
  const results = await Promise.allSettled(items.map((item) => firstValueFrom(remove(item))));
  const deleted: T[] = [];
  results.forEach((result, i) => {
    if (result.status === 'fulfilled') {
      deleted.push(items[i]);
    } else {
      console.error('Eliminazione non riuscita', result.reason);
    }
  });
  const failed = items.length - deleted.length;
  if (failed > 0) {
    snackBar.open(
      failed === items.length
        ? 'Eliminazione non riuscita. Riprova più tardi.'
        : `Non è stato possibile eliminare ${countLabel(failed)} su ${items.length}. Riprova più tardi.`,
    );
  }
  return deleted;
}
