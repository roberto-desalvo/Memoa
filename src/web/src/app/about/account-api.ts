import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';

/** Account dell'utente autenticato. */
@Injectable({ providedIn: 'root' })
export class AccountApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_CONFIG).apiBaseUrl}/me`;

  /** Elimina l'account e tutti i dati: note, file e impostazioni. */
  delete(): Observable<void> {
    return this.http.delete<void>(this.url);
  }
}
