import { Injectable, signal } from '@angular/core';

/**
 * Tag scelto tra i filtri della homepage (null: tutte le note). Resta in memoria tra una pagina e
 * l'altra, come la ricerca: aprendo una nota e tornando indietro il filtro è ancora lì.
 */
@Injectable({ providedIn: 'root' })
export class TagFilter {
  readonly tagUid = signal<string | null>(null);
}
