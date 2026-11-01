import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { SourceType } from '../records/records-api';

/** Frammento di una nota simile alla richiesta. */
export interface SearchResult {
  recordUid: string;
  title: string;
  /** Data e ora della nota, ISO 8601 in UTC. */
  occurredAt: string;
  sourceType: SourceType;
  /** Testo del frammento. */
  content: string;
  /** Similarità coseno, da 0 a 1. */
  similarity: number;
}

/** Limite sulla richiesta, come nelle Functions. */
export const MAX_QUERY_LENGTH = 1_000;

@Injectable({ providedIn: 'root' })
export class SearchApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_CONFIG).apiBaseUrl}/search`;

  /** La richiesta va nel corpo: mai testi dell'utente nell'URL. */
  search(query: string): Observable<SearchResult[]> {
    return this.http.post<SearchResult[]>(this.url, { query });
  }
}
