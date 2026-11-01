import { inject, Injectable, OnDestroy, signal } from '@angular/core';
import { APP_CONFIG } from '../config/app-config';

// Preferenza Opus/WebM, fallback MP4/AAC per i Safari precedenti alla 18.4.
const PREFERRED_TYPES = ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/mp4'];

/**
 * Registrazione dal microfono con MediaRecorder, nel formato nativo del browser e senza bitrate
 * personalizzato: la normalizzazione in Opus avviene dopo, nel job con ffmpeg.
 */
@Injectable()
export class AudioRecorder implements OnDestroy {
  /** Registrazione avviata e non ancora fermata, anche se in pausa. */
  readonly recording = signal(false);
  readonly paused = signal(false);
  readonly elapsedMs = signal(0);
  /** Durata massima di una registrazione, dalla configurazione. */
  readonly maxDurationMs = inject(APP_CONFIG).maxAudioDurationMinutes * 60 * 1000;

  private recorder?: MediaRecorder;
  private stream?: MediaStream;
  private timer?: ReturnType<typeof setInterval>;
  /** Durata dei tratti già conclusi da una pausa. */
  private accumulatedMs = 0;
  /** Inizio del tratto in corso; assente durante la pausa. */
  private segmentStartedAt?: number;

  static isSupported(): boolean {
    return typeof MediaRecorder !== 'undefined' && !!navigator.mediaDevices?.getUserMedia;
  }

  /**
   * Chiede il permesso del microfono e avvia la registrazione. `onRecorded` riceve l'audio quando
   * la registrazione si ferma, con {@link stop} o al raggiungimento della durata massima.
   */
  async start(onRecorded: (audio: Blob) => void): Promise<void> {
    this.stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    const mimeType = PREFERRED_TYPES.find((type) => MediaRecorder.isTypeSupported(type));
    let recorder: MediaRecorder;
    try {
      recorder = new MediaRecorder(this.stream, mimeType ? { mimeType } : undefined);
    } catch (err) {
      // Senza registratore il microfono va rilasciato subito.
      this.release();
      throw err;
    }
    const chunks: Blob[] = [];

    recorder.ondataavailable = (event) => {
      if (event.data.size > 0) {
        chunks.push(event.data);
      }
    };
    recorder.onstop = () => {
      this.release();
      // Il Blob prende il tipo effettivamente scelto dal browser.
      onRecorded(new Blob(chunks, { type: recorder.mimeType || mimeType || '' }));
    };

    this.recorder = recorder;
    recorder.start(1000);

    this.accumulatedMs = 0;
    this.segmentStartedAt = Date.now();
    this.elapsedMs.set(0);
    this.paused.set(false);
    this.recording.set(true);
    // Il tempo in pausa non conta per la durata massima.
    this.timer = setInterval(() => {
      if (this.segmentStartedAt === undefined) {
        return;
      }
      const elapsed = this.accumulatedMs + Date.now() - this.segmentStartedAt;
      this.elapsedMs.set(elapsed);
      if (elapsed >= this.maxDurationMs) {
        this.stop();
      }
    }, 250);
  }

  pause(): void {
    if (this.recorder?.state !== 'recording' || this.segmentStartedAt === undefined) {
      return;
    }
    this.recorder.pause();
    this.accumulatedMs += Date.now() - this.segmentStartedAt;
    this.segmentStartedAt = undefined;
    this.elapsedMs.set(this.accumulatedMs);
    this.paused.set(true);
  }

  resume(): void {
    if (this.recorder?.state !== 'paused') {
      return;
    }
    this.recorder.resume();
    this.segmentStartedAt = Date.now();
    this.paused.set(false);
  }

  stop(): void {
    // Anche in pausa: MediaRecorder si ferma e consegna l'audio registrato fin lì.
    if (this.recorder && this.recorder.state !== 'inactive') {
      this.recorder.stop();
    }
  }

  ngOnDestroy(): void {
    // Uscendo dalla pagina la registrazione in corso viene scartata.
    if (this.recorder) {
      this.recorder.onstop = null;
      this.stop();
    }
    this.release();
  }

  private release(): void {
    clearInterval(this.timer);
    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = undefined;
    this.recorder = undefined;
    this.segmentStartedAt = undefined;
    this.recording.set(false);
    this.paused.set(false);
  }
}
