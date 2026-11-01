import { Injectable } from '@angular/core';

/**
 * File scelti dalla barra di aggiunta della homepage, passati alla pagina di creazione: il selettore
 * dei file va aperto direttamente dal tap dell'utente, prima della navigazione.
 */
@Injectable({ providedIn: 'root' })
export class NoteDraft {
  private files: File[] = [];

  set(files: File[]): void {
    this.files = files;
  }

  /** Restituisce i file una sola volta. */
  take(): File[] {
    const files = this.files;
    this.files = [];
    return files;
  }
}
