import { Injectable, signal } from '@angular/core';

export type ThemePreference = 'system' | 'light' | 'dark';

const STORAGE_KEY = 'memoa.theme';

/**
 * Modalità chiara / scura: preferenza del dispositivo, salvata nel browser e non nell'account. Con
 * "system" segue l'impostazione del sistema operativo (prefers-color-scheme).
 */
@Injectable({ providedIn: 'root' })
export class Theme {
  readonly preference = signal<ThemePreference>(readPreference());

  constructor() {
    apply(this.preference());
  }

  set(preference: ThemePreference): void {
    this.preference.set(preference);
    apply(preference);
    try {
      localStorage.setItem(STORAGE_KEY, preference);
    } catch {
      // Storage non disponibile (es. navigazione privata): la scelta vale fino alla chiusura.
    }
  }
}

function readPreference(): ThemePreference {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return value === 'light' || value === 'dark' ? value : 'system';
  } catch {
    return 'system';
  }
}

function apply(preference: ThemePreference): void {
  if (preference === 'system') {
    document.documentElement.removeAttribute('data-theme');
  } else {
    document.documentElement.setAttribute('data-theme', preference);
  }
}
