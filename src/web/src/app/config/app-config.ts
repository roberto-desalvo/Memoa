import { InjectionToken } from '@angular/core';

/**
 * Configurazione dell'ambiente, letta a runtime da `config.json` (copia di `config.example.json`):
 * la stessa build vale per dev e prod e i client ID restano fuori dalla repo.
 */
export interface AppConfig {
  /** URL base delle Functions, es. http://localhost:7071/api. */
  apiBaseUrl: string;
  auth: {
    /** Authority del tenant esterno, es. https://<sottodominio>.ciamlogin.com/<tenant-id>. */
    authority: string;
    /** Client ID della registrazione del frontend (AUTH_WEB_CLIENT_ID_<ENV>). */
    clientId: string;
    /** Scope dell'API (AUTH_API_SCOPE_<ENV>). */
    apiScope: string;
  };
  /**
   * Durata massima di un audio in minuti, per la registrazione e per i file caricati. Nel deploy la
   * pipeline la copia dall'impostazione Audio__MaxDurationMinutes delle Functions (da Bicep).
   */
  maxAudioDurationMinutes: number;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');

export async function loadAppConfig(): Promise<AppConfig> {
  const response = await fetch('config.json', { cache: 'no-cache' });
  if (!response.ok) {
    throw new Error(`config.json non disponibile (HTTP ${response.status}).`);
  }
  return response.json();
}
