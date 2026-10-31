import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import {
  MSAL_GUARD_CONFIG,
  MSAL_INSTANCE,
  MSAL_INTERCEPTOR_CONFIG,
  MsalBroadcastService,
  MsalGuard,
  MsalGuardConfiguration,
  MsalInterceptor,
  MsalInterceptorConfiguration,
  MsalService,
} from '@azure/msal-angular';
import {
  BrowserCacheLocation,
  InteractionType,
  IPublicClientApplication,
  PublicClientApplication,
} from '@azure/msal-browser';
import { AppConfig } from '../config/app-config';

/** Crea e inizializza l'istanza MSAL: va fatto prima di qualunque altra chiamata a MSAL. */
export async function createMsalInstance(config: AppConfig): Promise<IPublicClientApplication> {
  const instance = new PublicClientApplication({
    auth: {
      clientId: config.auth.clientId,
      authority: config.auth.authority,
      // Entra External ID (ciamlogin.com) non è tra le authority che MSAL conosce già.
      knownAuthorities: [new URL(config.auth.authority).hostname],
      redirectUri: window.location.origin,
      postLogoutRedirectUri: window.location.origin,
    },
    cache: { cacheLocation: BrowserCacheLocation.LocalStorage },
  });
  await instance.initialize();
  return instance;
}

/**
 * Login con redirect: le route con MsalGuard richiedono un utente autenticato e MsalInterceptor
 * aggiunge il token di accesso alle chiamate verso le Functions.
 */
export function provideMsal(
  config: AppConfig,
  instance: IPublicClientApplication,
): EnvironmentProviders {
  const scopes = [config.auth.apiScope];

  const guardConfig: MsalGuardConfiguration = {
    interactionType: InteractionType.Redirect,
    authRequest: { scopes },
  };

  const interceptorConfig: MsalInterceptorConfiguration = {
    interactionType: InteractionType.Redirect,
    protectedResourceMap: new Map([[`${config.apiBaseUrl}/*`, scopes]]),
  };

  return makeEnvironmentProviders([
    { provide: MSAL_INSTANCE, useValue: instance },
    { provide: MSAL_GUARD_CONFIG, useValue: guardConfig },
    { provide: MSAL_INTERCEPTOR_CONFIG, useValue: interceptorConfig },
    { provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true },
    MsalService,
    MsalGuard,
    MsalBroadcastService,
  ]);
}
