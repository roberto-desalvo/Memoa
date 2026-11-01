import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';

export type RecordStatus = 'Processing' | 'Ready' | 'Error';
export type SourceType = 'Audio' | 'Text' | 'Image';
/** Passo in corso di un record in elaborazione. */
export type ProcessingStep = 'Preparing' | 'Transcribing' | 'Indexing';

/** Record nella lista della homepage. */
export interface RecordSummary {
  recordUid: string;
  title: string;
  /** Data e ora della nota, ISO 8601 in UTC. */
  occurredAt: string;
  sourceType: SourceType;
  status: RecordStatus;
  canRetry: boolean;
  /**
   * Inizio del testo (al massimo EXCERPT_LENGTH caratteri), null se il record non ha ancora un testo.
   * Facoltativo finché le Functions distribuite non lo restituiscono.
   */
  excerpt?: string | null;
  /** Durata dell'audio in secondi, null senza audio o finché non è normalizzato. Facoltativa come excerpt. */
  durationSeconds?: number | null;
  /** Tag assegnati, in ordine di nome. Facoltativi come excerpt. */
  tags?: RecordTag[];
}

/** Tag assegnato a un record, in automatico (per similarità) o dall'utente. */
export interface RecordTag {
  tagUid: string;
  name: string;
  isAutomatic: boolean;
  /** Falso per un tag disattivato, che conserva le assegnazioni. */
  isActive: boolean;
}

/** Lunghezza dell'estratto nella lista, uguale a quella delle Functions (ListRecordsFunction). */
export const EXCERPT_LENGTH = 200;

/** Record nella propria pagina: il testo è completo, l'estratto non c'è. */
export interface RecordDetail extends Omit<RecordSummary, 'excerpt'> {
  text: string | null;
  language: string | null;
  hasAudio: boolean;
  durationSeconds: number | null;
  /** Il testo è la trascrizione automatica di un audio. */
  isTranscript: boolean;
  /** La trascrizione automatica è stata modificata dall'utente. */
  isTranscriptEdited: boolean;
  canRegenerateTranscript: boolean;
  /** Null se il record non è in elaborazione. */
  processingStep: ProcessingStep | null;
}

/** Titolo e data della nota, chiesti per tutte le modalità di aggiunta. */
export interface NoteDetails {
  title: string;
  /** ISO 8601 in UTC. */
  occurredAt: string;
}

export interface RecordChanges {
  title?: string;
  occurredAt?: string;
  text?: string;
}

/** Limite sul testo di un record, come nelle Functions. */
export const MAX_TEXT_LENGTH = 50_000;

/** Chiamate alle Functions sui record dell'utente; il token lo aggiunge MsalInterceptor. */
@Injectable({ providedIn: 'root' })
export class RecordsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(APP_CONFIG).apiBaseUrl}/records`;

  list(): Observable<RecordSummary[]> {
    return this.http.get<RecordSummary[]>(this.baseUrl);
  }

  get(recordUid: string): Observable<RecordDetail> {
    return this.http.get<RecordDetail>(`${this.baseUrl}/${recordUid}`);
  }

  /** URL con SAS di sola lettura della copia normalizzata. */
  audio(recordUid: string): Observable<{ url: string; expiresOn: string }> {
    return this.http.get<{ url: string; expiresOn: string }>(`${this.baseUrl}/${recordUid}/audio`);
  }

  createText(note: NoteDetails & { text: string }): Observable<RecordDetail> {
    return this.http.post<RecordDetail>(`${this.baseUrl}/text`, note);
  }

  update(recordUid: string, changes: RecordChanges): Observable<RecordDetail> {
    return this.http.patch<RecordDetail>(`${this.baseUrl}/${recordUid}`, changes);
  }

  delete(recordUid: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${recordUid}`);
  }

  retry(recordUid: string): Observable<RecordDetail> {
    return this.http.post<RecordDetail>(`${this.baseUrl}/${recordUid}/retry`, null);
  }

  /** Rifà la trascrizione automatica dalla copia normalizzata; sostituisce il testo corrente. */
  regenerateTranscript(recordUid: string): Observable<RecordDetail> {
    return this.http.post<RecordDetail>(`${this.baseUrl}/${recordUid}/transcript/regenerate`, null);
  }

  /**
   * Tutti i tag che il record deve mostrare: quelli aggiunti diventano manuali, quelli tolti non vengono
   * più assegnati in automatico.
   */
  setTags(recordUid: string, tagUids: string[]): Observable<RecordDetail> {
    return this.http.put<RecordDetail>(`${this.baseUrl}/${recordUid}/tags`, { tagUids });
  }
}
