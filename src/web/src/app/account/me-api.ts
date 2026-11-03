import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';

/** Utente applicativo, restituito da `GET /me`. */
export interface Me {
  /** La guida va proposta: l'utente non l'ha ancora completata né rifiutata. */
  showGuide: boolean;
  /** L'utente può modificare i parametri di sistema (pagina di amministrazione). */
  isAdmin: boolean;
}

/**
 * Codice d'errore (con 403) delle Functions quando l'utente applicativo non esiste ancora: lo crea solo
 * `GET /me`.
 */
export const USER_NOT_REGISTERED = 'user_not_registered';

@Injectable({ providedIn: 'root' })
export class MeApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_CONFIG).apiBaseUrl}/me`;

  /** Registra l'utente applicativo se non esiste ancora. */
  get(): Observable<Me> {
    return this.http.get<Me>(this.url);
  }

  /** La guida è stata completata o rifiutata: non verrà più proposta. */
  dismissGuide(): Observable<void> {
    return this.http.put<void>(`${this.url}/guide`, { dismissed: true });
  }
}
