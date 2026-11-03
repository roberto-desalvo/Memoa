import { Routes } from '@angular/router';
import { MsalGuard } from '@azure/msal-angular';
import { AboutPage } from './about/about-page';
import { AdminPage } from './admin/admin-page';
import { adminGuard } from './admin/admin-guard';
import { Home } from './home/home';
import { NewNote } from './notes/new-note';
import { deletedRecordGuard } from './records/deleted-record-guard';
import { RecordPage } from './records/record-page';
import { SettingsPage } from './settings/settings-page';
import { TagPage } from './tags/tag-page';
import { TagsPage } from './tags/tags-page';

export const routes: Routes = [
  { path: '', component: Home, canActivate: [MsalGuard] },
  { path: 'new/:mode', component: NewNote, canActivate: [MsalGuard] },
  {
    path: 'records/:recordUid',
    component: RecordPage,
    canActivate: [MsalGuard, deletedRecordGuard],
  },
  { path: 'tags', component: TagsPage, canActivate: [MsalGuard] },
  { path: 'tags/new', component: TagPage, canActivate: [MsalGuard] },
  { path: 'tags/:tagUid', component: TagPage, canActivate: [MsalGuard] },
  { path: 'settings', component: SettingsPage, canActivate: [MsalGuard] },
  { path: 'admin', component: AdminPage, canActivate: [MsalGuard, adminGuard] },
  { path: 'about', component: AboutPage, canActivate: [MsalGuard] },
  { path: '**', redirectTo: '' },
];
