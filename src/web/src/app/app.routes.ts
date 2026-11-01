import { Routes } from '@angular/router';
import { MsalGuard } from '@azure/msal-angular';
import { Home } from './home/home';
import { NewNote } from './notes/new-note';
import { RecordPage } from './records/record-page';
import { SettingsPage } from './settings/settings-page';

export const routes: Routes = [
  { path: '', component: Home, canActivate: [MsalGuard] },
  { path: 'new/:mode', component: NewNote, canActivate: [MsalGuard] },
  { path: 'records/:recordUid', component: RecordPage, canActivate: [MsalGuard] },
  { path: 'settings', component: SettingsPage, canActivate: [MsalGuard] },
  { path: '**', redirectTo: '' },
];
