import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import {
  ApplicationConfig,
  isDevMode,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MAT_CARD_CONFIG } from '@angular/material/card';
import { MAT_DIALOG_DEFAULT_OPTIONS, MatDialogConfig } from '@angular/material/dialog';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MAT_SNACK_BAR_DEFAULT_OPTIONS, MatSnackBarConfig } from '@angular/material/snack-bar';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';
import { IPublicClientApplication } from '@azure/msal-browser';
import { routes } from './app.routes';
import { provideMsal } from './auth/msal';
import { APP_CONFIG, AppConfig } from './config/app-config';
import { registerIcons } from './shared/icons';
import { enablePageSwipe, slideBetweenPages } from './shared/page-slide';

export function appConfig(
  config: AppConfig,
  msalInstance: IPublicClientApplication,
): ApplicationConfig {
  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      // I parametri del percorso arrivano ai componenti come input (es. recordUid).
      // Home e Tag passano dall'una all'altra scorrendo lateralmente (shared/page-slide.ts).
      provideRouter(
        routes,
        withComponentInputBinding(),
        withViewTransitions({ onViewTransitionCreated: slideBetweenPages }),
      ),
      // MsalInterceptor è un interceptor di classe: va registrato tramite DI.
      provideHttpClient(withInterceptorsFromDi()),
      { provide: APP_CONFIG, useValue: config },
      provideMsal(config, msalInstance),
      provideAppInitializer(registerIcons),
      provideAppInitializer(enablePageSwipe),
      // PWA installabile: il service worker mette in cache solo l'app (ngsw-config.json), mai i dati
      // delle Functions né config.json. Registrato quando l'app è stabile, per non rallentare l'avvio.
      provideServiceWorker('ngsw-worker.js', {
        enabled: !isDevMode(),
        registrationStrategy: 'registerWhenStable:30000',
      }),
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
