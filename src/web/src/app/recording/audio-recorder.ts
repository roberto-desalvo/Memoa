import { Injectable, OnDestroy, signal } from '@angular/core';

/** Durata massima di una registrazione. */
export const MAX_RECORDING_MS = 30 * 60 * 1000;

// Preferenza Opus/WebM, fallback MP4/AAC per i Safari precedenti alla 18.4.
const PREFERRED_TYPES = ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/mp4'];

/**
 * Registrazione dal microfono con MediaRecorder, nel formato nativo del browser e senza bitrate
 * personalizzato: la normalizzazione in Opus avviene dopo, nel job con ffmpeg.
 */
@Injectable()
export class AudioRecorder implements OnDestroy {
  readonly recording = signal(false);
  readonly elapsedMs = signal(0);

  private recorder?: MediaRecorder;
  private stream?: MediaStream;
  private timer?: ReturnType<typeof setInterval>;

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

    const startedAt = Date.now();
    this.elapsedMs.set(0);
    this.recording.set(true);
    this.timer = setInterval(() => {
      const elapsed = Date.now() - startedAt;
      this.elapsedMs.set(elapsed);
      if (elapsed >= MAX_RECORDING_MS) {
        this.stop();
      }
    }, 250);
  }

  stop(): void {
    if (this.recorder?.state === 'recording') {
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
    this.recording.set(false);
  }
}
