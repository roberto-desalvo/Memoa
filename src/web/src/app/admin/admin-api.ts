import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';

/** Parametri di sistema; null vale "usa la configurazione delle Functions". */
export interface SystemSettingsValues {
  speechEnabled: boolean | null;
  speechBaseTimeoutSeconds: number | null;
  speechTimeoutPerAudioMinuteSeconds: number | null;
  searchMinSimilarity: number | null;
  searchMaxResults: number | null;
  taggingMinSimilarity: number | null;
  taggingPendingTimeoutMinutes: number | null;
}

/** Risposta di `GET /system/settings`, solo per gli amministratori. */
export interface SystemSettings {
  /** Valori salvati nel database. */
  values: SystemSettingsValues;
  /** Valori della configurazione, usati dove `values` è null: mai null. */
  defaults: SystemSettingsValues;
  /** Ultimo salvataggio, null se i parametri non sono mai stati salvati. */
  updatedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_CONFIG).apiBaseUrl}/system/settings`;

  get(): Observable<SystemSettings> {
    return this.http.get<SystemSettings>(this.url);
  }

  update(values: SystemSettingsValues): Observable<SystemSettings> {
    return this.http.put<SystemSettings>(this.url, values);
  }
}
