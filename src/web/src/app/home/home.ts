import { JsonPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { APP_CONFIG } from '../config/app-config';

interface MeResponse {
  userId: string;
}

@Component({
  selector: 'app-home',
  imports: [JsonPipe],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  private readonly msal = inject(MsalService);
  private readonly config = inject(APP_CONFIG);

  protected readonly accountName =
    (this.msal.instance.getActiveAccount() ?? this.msal.instance.getAllAccounts()[0])?.name;

  // Chiamata di prova all'endpoint autenticato delle Functions: verifica il giro del token.
  protected readonly me = httpResource<MeResponse>(() => `${this.config.apiBaseUrl}/me`);

  protected logout(): void {
    this.msal.logoutRedirect();
  }
}
