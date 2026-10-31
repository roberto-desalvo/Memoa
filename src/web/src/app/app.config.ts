import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { IPublicClientApplication } from '@azure/msal-browser';
import { routes } from './app.routes';
import { provideMsal } from './auth/msal';
import { APP_CONFIG, AppConfig } from './config/app-config';

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
    ],
  };
}
