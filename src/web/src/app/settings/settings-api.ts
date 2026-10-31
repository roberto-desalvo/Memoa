import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';

export type Language = 'it' | 'en';

/** Impostazioni dell'account salvate nel database. */
export interface AccountSettings {
  /** Lingua preferita: per ora fa da ripiego per la trascrizione; l'interfaccia resta in italiano. */
  preferredLanguage: Language;
}

@Injectable({ providedIn: 'root' })
export class SettingsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_CONFIG).apiBaseUrl}/me/settings`;

  get(): Observable<AccountSettings> {
    return this.http.get<AccountSettings>(this.url);
  }

  update(settings: AccountSettings): Observable<AccountSettings> {
    return this.http.put<AccountSettings>(this.url, settings);
  }
}
