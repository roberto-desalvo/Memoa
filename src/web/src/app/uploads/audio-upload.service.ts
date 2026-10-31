import { HttpClient, HttpEventType, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { filter, map, Observable, switchMap } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';

interface CreateAudioUploadResponse {
  recordId: string;
  uploadUrl: string;
  expiresOn: string;
}

export type UploadProgress =
  { kind: 'progress'; fraction: number } | { kind: 'done'; recordId: string };

/**
 * Upload diretto su Blob Storage: le Functions restituiscono un URL con SAS valido solo per creare
 * quel blob, poi il file va allo storage senza passare dalle Functions.
 */
@Injectable({ providedIn: 'root' })
export class AudioUploadService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(APP_CONFIG);

  upload(audio: Blob): Observable<UploadProgress> {
    // Il token di accesso lo aggiunge MsalInterceptor; l'URL dello storage non è tra le sue
    // risorse protette, quindi il PUT parte solo con il SAS.
    return this.http
      .post<CreateAudioUploadResponse>(`${this.config.apiBaseUrl}/uploads/audio`, {
        sizeBytes: audio.size,
      })
      .pipe(switchMap(({ recordId, uploadUrl }) => this.put(uploadUrl, audio, recordId)));
  }

  // Put Blob in una sola richiesta: adeguato alle registrazioni (al massimo 30 minuti, poche
  // decine di MB). Per i file caricati fino a 350 MB servirà l'upload a blocchi.
  private put(uploadUrl: string, audio: Blob, recordId: string): Observable<UploadProgress> {
    const headers = new HttpHeaders({
      'x-ms-blob-type': 'BlockBlob',
      'x-ms-blob-content-type': audio.type || 'application/octet-stream',
    });

    return this.http
      .put(uploadUrl, audio, { headers, observe: 'events', reportProgress: true })
      .pipe(
        filter(
          (event) =>
            event.type === HttpEventType.UploadProgress || event.type === HttpEventType.Response,
        ),
        map((event): UploadProgress =>
          event.type === HttpEventType.UploadProgress
            ? { kind: 'progress', fraction: event.total ? event.loaded / event.total : 0 }
            : { kind: 'done', recordId },
        ),
      );
  }
}
