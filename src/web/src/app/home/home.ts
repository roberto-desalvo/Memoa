import { Component, inject } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { RecordAudio } from '../recording/record-audio';

@Component({
  selector: 'app-home',
  imports: [RecordAudio],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  private readonly msal = inject(MsalService);

  protected readonly accountName = (
    this.msal.instance.getActiveAccount() ?? this.msal.instance.getAllAccounts()[0]
  )?.name;

  protected logout(): void {
    this.msal.logoutRedirect();
  }
}
