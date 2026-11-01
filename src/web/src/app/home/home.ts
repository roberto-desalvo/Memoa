import { Component, computed, inject, OnDestroy, OnInit } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink } from '@angular/router';
import { MsalService } from '@azure/msal-angular';
import { firstValueFrom } from 'rxjs';
import { dayKey, dayLabel, NoteDatePipe } from '../notes/note-date';
import { NoteDraft } from '../notes/note-draft';
import { RecordsApi, RecordSummary } from '../records/records-api';
import { RecordsStore } from '../records/records-store';
import { DELETE_NOTE_DIALOG, openConfirmDialog } from '../shared/confirm-dialog';
import { AUDIO_ACCEPT } from '../uploads/audio-formats';
import { TEXT_ACCEPT } from '../uploads/text-file';

@Component({
  selector: 'app-home',
  imports: [
    RouterLink,
    NoteDatePipe,
    NgTemplateOutlet,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatToolbarModule,
  ],
  templateUrl: './home.html',
  styleUrl: './home.scss',
  host: { class: 'page' },
})
export class Home implements OnInit, OnDestroy {
  private readonly msal = inject(MsalService);
  private readonly router = inject(Router);
  private readonly api = inject(RecordsApi);
  private readonly drafts = inject(NoteDraft);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly store = inject(RecordsStore);

  protected readonly accountName = (
    this.msal.instance.getActiveAccount() ?? this.msal.instance.getAllAccounts()[0]
  )?.name;

  /** Iniziali del nome visualizzato, mostrate nel pulsante del menu utente. */
  protected readonly initials = (this.accountName ?? '')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('');

  protected readonly uploadAccept = `${AUDIO_ACCEPT},${TEXT_ACCEPT}`;

  /** Record raggruppati per giorno della nota, nell'ordine della lista (dal più recente). */
  protected readonly groups = computed(() => groupByDay(this.store.records() ?? []));

  ngOnInit(): void {
    this.store.start();
  }

  ngOnDestroy(): void {
    this.store.stop();
  }

  protected logout(): void {
    this.msal.logoutRedirect();
  }

  protected selectFile(input: HTMLInputElement): void {
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (files.length > 0) {
      this.drafts.set(files);
      void this.router.navigate(['/new', 'upload']);
    }
  }

  /** I record in errore non si aprono: il tap mostra la finestra con "Riprova" ed "Elimina". */
  protected async openError(record: RecordSummary): Promise<void> {
    const result = await openConfirmDialog(this.dialog, {
      title: 'Errore nell’elaborazione',
      message: record.canRetry
        ? 'Si è verificato un errore durante l’elaborazione di questa nota. Vuoi riprovare?'
        : 'Il file non è valido e non può essere elaborato.',
      confirm: record.canRetry ? 'Riprova' : undefined,
      secondary: 'Elimina',
      cancel: 'Chiudi',
    });
    if (result === 'confirm') {
      await this.retry(record);
    } else if (result === 'secondary') {
      await this.delete(record);
    }
  }

  private async retry(record: RecordSummary): Promise<void> {
    try {
      this.store.replace(await firstValueFrom(this.api.retry(record.recordUid)));
    } catch (err) {
      console.error('Nuovo tentativo non riuscito', err);
      this.snackBar.open('Operazione non riuscita. Riprova più tardi.');
    }
  }

  private async delete(record: RecordSummary): Promise<void> {
    if ((await openConfirmDialog(this.dialog, DELETE_NOTE_DIALOG)) !== 'confirm') {
      return;
    }
    try {
      await firstValueFrom(this.api.delete(record.recordUid));
      this.store.remove(record.recordUid);
    } catch (err) {
      console.error('Eliminazione non riuscita', err);
      this.snackBar.open('Eliminazione non riuscita. Riprova più tardi.');
    }
  }
}

interface DayGroup {
  key: string;
  label: string;
  records: RecordSummary[];
}

/**
 * Un gruppo per giorno, nell'ordine in cui il giorno compare nella lista. Raggruppa per chiave e non
 * per record consecutivi: un record con la data appena modificata non crea un gruppo doppio.
 */
function groupByDay(records: RecordSummary[]): DayGroup[] {
  const now = new Date();
  const groups = new Map<string, DayGroup>();
  for (const record of records) {
    const date = new Date(record.occurredAt);
    const key = dayKey(date);
    let group = groups.get(key);
    if (!group) {
      group = { key, label: dayLabel(date, now), records: [] };
      groups.set(key, group);
    }
    group.records.push(record);
  }
  return [...groups.values()];
}
