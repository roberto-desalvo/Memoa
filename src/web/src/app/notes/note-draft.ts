import { Injectable } from '@angular/core';

/**
 * File scelto dalla barra di aggiunta della homepage, passato alla pagina di creazione: il selettore
 * dei file va aperto direttamente dal tap dell'utente, prima della navigazione.
 */
@Injectable({ providedIn: 'root' })
export class NoteDraft {
  private file?: File;

  set(file: File): void {
    this.file = file;
  }

  /** Restituisce il file una sola volta. */
  take(): File | undefined {
    const file = this.file;
    this.file = undefined;
    return file;
  }
}
