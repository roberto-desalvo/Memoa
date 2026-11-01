import { Component, ElementRef, inject, OnDestroy, OnInit, signal, viewChild } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MsalService } from '@azure/msal-angular';
import { firstValueFrom } from 'rxjs';
import { NoteDatePipe } from '../notes/note-date';
import { NoteDraft } from '../notes/note-draft';
import { RecordsApi, RecordSummary } from '../records/records-api';
import { RecordsStore } from '../records/records-store';
import { AUDIO_ACCEPT } from '../uploads/audio-formats';
import { TEXT_ACCEPT } from '../uploads/text-file';

/** Azione in corso nella modale di un record in errore. */
type ErrorDialogState = 'idle' | 'busy' | 'confirmDelete' | 'failed';

@Component({
  selector: 'app-home',
  imports: [RouterLink, NgTemplateOutlet, NoteDatePipe],
  templateUrl: './home.html',
  styleUrl: './home.scss',
  host: { class: 'page' },
})
export class Home implements OnInit, OnDestroy {
  private readonly msal = inject(MsalService);
  private readonly router = inject(Router);
  private readonly api = inject(RecordsApi);
  private readonly drafts = inject(NoteDraft);
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
  protected readonly menuOpen = signal(false);
  protected readonly addOpen = signal(false);

  protected readonly errorRecord = signal<RecordSummary | null>(null);
  protected readonly errorState = signal<ErrorDialogState>('idle');
  private readonly errorDialog = viewChild.required<ElementRef<HTMLDialogElement>>('errorDialog');

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

  /** I record in errore sono disabilitati, ma il tap apre la modale con "riprova". */
  protected openError(record: RecordSummary): void {
    this.errorRecord.set(record);
    this.errorState.set('idle');
    this.errorDialog().nativeElement.showModal();
  }

  protected closeError(): void {
    this.errorDialog().nativeElement.close();
  }

  protected async retry(record: RecordSummary): Promise<void> {
    this.errorState.set('busy');
    try {
      this.store.replace(await firstValueFrom(this.api.retry(record.recordUid)));
      this.closeError();
    } catch (err) {
      console.error('Nuovo tentativo non riuscito', err);
      this.errorState.set('failed');
    }
  }

  protected async delete(record: RecordSummary): Promise<void> {
    this.errorState.set('busy');
    try {
      await firstValueFrom(this.api.delete(record.recordUid));
      this.store.remove(record.recordUid);
      this.closeError();
    } catch (err) {
      console.error('Eliminazione non riuscita', err);
      this.errorState.set('failed');
    }
  }
}
