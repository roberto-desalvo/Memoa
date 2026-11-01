import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { RecordSummary } from '../records/records-api';

/** Tag nella pagina dei tag. */
export interface TagSummary {
  tagUid: string;
  name: string;
  description: string | null;
  /** Un tag disattivato non viene più assegnato in automatico. */
  isActive: boolean;
  /** Note a cui è assegnato. */
  recordCount: number;
}

/** Tag nella propria pagina, con le note a cui è assegnato. */
export interface TagDetail extends Omit<TagSummary, 'recordCount'> {
  records: RecordSummary[];
}

export interface TagChanges {
  name?: string;
  /** Una stringa vuota toglie la descrizione. */
  description?: string;
  isActive?: boolean;
}

/** Limiti di nome e descrizione, come nelle Functions. */
export const MAX_TAG_NAME_LENGTH = 50;
export const MAX_TAG_DESCRIPTION_LENGTH = 2_000;

/** Chiamate alle Functions sui tag dell'utente; il token lo aggiunge MsalInterceptor. */
@Injectable({ providedIn: 'root' })
export class TagsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(APP_CONFIG).apiBaseUrl}/tags`;

  list(): Observable<TagSummary[]> {
    return this.http.get<TagSummary[]>(this.baseUrl);
  }

  get(tagUid: string): Observable<TagDetail> {
    return this.http.get<TagDetail>(`${this.baseUrl}/${tagUid}`);
  }

  /** L'embedding si calcola subito; poi il tag viene applicato alle note esistenti. */
  create(tag: { name: string; description?: string }): Observable<TagSummary> {
    return this.http.post<TagSummary>(this.baseUrl, tag);
  }

  update(tagUid: string, changes: TagChanges): Observable<TagSummary> {
    return this.http.patch<TagSummary>(`${this.baseUrl}/${tagUid}`, changes);
  }

  delete(tagUid: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${tagUid}`);
  }
}

/** Messaggio per un errore nel salvataggio di un tag, dal codice restituito dalle Functions. */
export function tagSaveError(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) {
    return 'Salvataggio non riuscito.';
  }
  if (err.status === 409) {
    return err.error?.error === 'name_taken'
      ? 'Esiste già un tag con questo nome.'
      : 'Il tag è stato aggiornato nel frattempo: ricarica la pagina e riprova.';
  }
  if (err.status === 503) {
    return 'Servizio non disponibile. Riprova tra poco.';
  }
  return 'Salvataggio non riuscito.';
}
