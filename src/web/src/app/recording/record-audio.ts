import { Component, computed, inject, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { AudioRecorder } from './audio-recorder';

/**
 * Registrazione dal microfono. Emette l'audio quando la registrazione si ferma; titolo, data e
 * caricamento li gestisce la pagina di creazione.
 */
@Component({
  selector: 'app-record-audio',
  imports: [MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: './record-audio.html',
  styleUrl: './record-audio.scss',
  providers: [AudioRecorder],
})
export class RecordAudio {
  private readonly recorder = inject(AudioRecorder);

  readonly started = output<void>();
  readonly recorded = output<Blob>();

  protected readonly supported = AudioRecorder.isSupported();
  protected readonly recording = this.recorder.recording;
  protected readonly paused = this.recorder.paused;
  protected readonly error = signal<string | null>(null);
  protected readonly elapsed = computed(() => formatDuration(this.recorder.elapsedMs()));
  protected readonly maxDuration = formatDuration(this.recorder.maxDurationMs);

  protected async toggle(): Promise<void> {
    if (this.recording()) {
      this.recorder.stop();
      return;
    }

    this.error.set(null);
    try {
      await this.recorder.start((audio) => {
        if (audio.size === 0) {
          this.error.set('La registrazione è vuota.');
          return;
        }
        this.recorded.emit(audio);
      });
      this.started.emit();
    } catch (err) {
      console.error('Avvio della registrazione non riuscito', err);
      this.error.set(startErrorMessage(err));
    }
  }

  protected togglePause(): void {
    if (this.paused()) {
      this.recorder.resume();
    } else {
      this.recorder.pause();
    }
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
