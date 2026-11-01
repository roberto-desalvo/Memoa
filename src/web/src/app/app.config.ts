import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import {
  ApplicationConfig,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MAT_CARD_CONFIG } from '@angular/material/card';
import { MAT_DIALOG_DEFAULT_OPTIONS, MatDialogConfig } from '@angular/material/dialog';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MAT_SNACK_BAR_DEFAULT_OPTIONS, MatSnackBarConfig } from '@angular/material/snack-bar';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { IPublicClientApplication } from '@azure/msal-browser';
import { routes } from './app.routes';
import { provideMsal } from './auth/msal';
import { APP_CONFIG, AppConfig } from './config/app-config';
import { registerIcons } from './shared/icons';

export function appConfig(
  config: AppConfig,
  msalInstance: IPublicClientApplication,
): ApplicationConfig {
  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      // I parametri del percorso arrivano ai componenti come input (es. recordUid).
      provideRouter(routes, withComponentInputBinding()),
      // MsalInterceptor è un interceptor di classe: va registrato tramite DI.
      provideHttpClient(withInterceptorsFromDi()),
      { provide: APP_CONFIG, useValue: config },
      provideMsal(config, msalInstance),
      provideAppInitializer(registerIcons),
      // Un solo stile per componente (docs/project/ui.md, Regole visive).
      { provide: MAT_CARD_CONFIG, useValue: { appearance: 'outlined' } },
      { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'fill' } },
      // Il focus va sulla finestra, non sul primo pulsante: evita di attivare "Elimina" per errore.
      {
        provide: MAT_DIALOG_DEFAULT_OPTIONS,
        useValue: { ...new MatDialogConfig(), autoFocus: 'dialog' },
      },
      {
        provide: MAT_SNACK_BAR_DEFAULT_OPTIONS,
        useValue: { ...new MatSnackBarConfig(), duration: 5000 },
      },
    ],
  };
}
