import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AudioUploadService } from '../uploads/audio-upload.service';
import { AudioRecorder, MAX_RECORDING_MS } from './audio-recorder';

type Status =
  | { kind: 'idle' }
  | { kind: 'recording' }
  | { kind: 'uploading'; fraction: number }
  | { kind: 'done' }
  | { kind: 'error'; message: string; audio?: Blob };

@Component({
  selector: 'app-record-audio',
  templateUrl: './record-audio.html',
  styleUrl: './record-audio.scss',
  providers: [AudioRecorder],
})
export class RecordAudio {
  private readonly recorder = inject(AudioRecorder);
  private readonly uploads = inject(AudioUploadService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly supported = AudioRecorder.isSupported();
  protected readonly status = signal<Status>({ kind: 'idle' });
  protected readonly elapsed = computed(() => formatDuration(this.recorder.elapsedMs()));
  protected readonly maxDuration = formatDuration(MAX_RECORDING_MS);

  protected async toggle(): Promise<void> {
    if (this.status().kind === 'recording') {
      this.recorder.stop();
      return;
    }

    try {
      await this.recorder.start((audio) => this.upload(audio));
      this.status.set({ kind: 'recording' });
    } catch (err) {
      console.error('Avvio della registrazione non riuscito', err);
      this.status.set({ kind: 'error', message: startErrorMessage(err) });
    }
  }

  protected upload(audio: Blob): void {
    if (audio.size === 0) {
      this.status.set({ kind: 'error', message: 'La registrazione è vuota.' });
      return;
    }

    this.status.set({ kind: 'uploading', fraction: 0 });
    this.uploads
      .upload(audio)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (progress) =>
          this.status.set(
            progress.kind === 'progress'
              ? { kind: 'uploading', fraction: progress.fraction }
              : { kind: 'done' },
          ),
        // L'audio resta in memoria per riprovare senza registrare di nuovo.
        error: () =>
          this.status.set({ kind: 'error', message: 'Caricamento non riuscito.', audio }),
      });
  }
}

function startErrorMessage(err: unknown): string {
  const name = err instanceof Error ? err.name : '';
  switch (name) {
    case 'NotAllowedError':
      return 'Permesso del microfono negato: abilitalo nelle impostazioni del browser.';
    case 'NotFoundError':
      return 'Nessun microfono trovato.';
    case 'NotReadableError':
      return 'Il microfono è in uso da un’altra applicazione o non è accessibile.';
    case 'SecurityError':
      return 'Il microfono è disponibile solo su HTTPS o su localhost.';
    default:
      return `Impossibile avviare la registrazione${name ? ` (${name})` : ''}.`;
  }
}

function formatDuration(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}
