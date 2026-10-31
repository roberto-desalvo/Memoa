import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { appConfig } from './app/app.config';
import { createMsalInstance } from './app/auth/msal';
import { loadAppConfig } from './app/config/app-config';

async function main(): Promise<void> {
  const config = await loadAppConfig();
  const msalInstance = await createMsalInstance(config);
  await bootstrapApplication(App, appConfig(config, msalInstance));
}

main().catch((err) => console.error(err));
