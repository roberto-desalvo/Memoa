import { Component, ElementRef, inject, input, OnInit, signal, viewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { inputValueOf, NoteDatePipe, toOccurredAt } from '../notes/note-date';
import { MAX_TEXT_LENGTH, RecordChanges, RecordDetail, RecordsApi } from './records-api';
import { RecordsStore } from './records-store';

/**
 * Pagina di un record pronto: audio normalizzato riproducibile, testo, modifica ed eliminazione.
 * L'audio si riproduce tramite un SAS di sola lettura chiesto alle Functions.
 */
@Component({
  selector: 'app-record-page',
  imports: [RouterLink, NoteDatePipe],
  templateUrl: './record-page.html',
  styleUrl: './record-page.scss',
  host: { class: 'page' },
})
export class RecordPage implements OnInit {
  private readonly api = inject(RecordsApi);
  private readonly store = inject(RecordsStore);
  private readonly router = inject(Router);

  /** Dal percorso /records/:recordUid. */
  readonly recordUid = input.required<string>();

  protected readonly record = signal<RecordDetail | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly audioUrl = signal<string | null>(null);

  protected readonly editing = signal(false);
  protected readonly editTitle = signal('');
  protected readonly editDate = signal('');
  protected readonly editText = signal('');
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly maxTextLength = MAX_TEXT_LENGTH;

  private readonly deleteDialog = viewChild.required<ElementRef<HTMLDialogElement>>('deleteDialog');

  async ngOnInit(): Promise<void> {
    try {
      const record = await firstValueFrom(this.api.get(this.recordUid()));
      this.record.set(record);
      if (record.status === 'Ready' && record.hasAudio) {
        const { url } = await firstValueFrom(this.api.audio(record.recordUid));
        this.audioUrl.set(url);
      }
    } catch (err) {
      console.error('Caricamento del record non riuscito', err);
      this.loadError.set(
        err instanceof HttpErrorResponse && err.status === 404
          ? 'Nota non trovata.'
          : 'Impossibile caricare la nota.',
      );
    }
  }

  protected startEdit(record: RecordDetail): void {
    this.editTitle.set(record.title);
    this.editDate.set(inputValueOf(record.occurredAt));
    this.editText.set(record.text ?? '');
    this.actionError.set(null);
    this.editing.set(true);
  }

  protected async saveEdit(record: RecordDetail): Promise<void> {
    const title = this.editTitle().trim();
    if (!title || !this.editDate()) {
      this.actionError.set('Titolo e data sono obbligatori.');
      return;
    }

    const changes: RecordChanges = {};
    if (title !== record.title) {
      changes.title = title;
    }
    if (this.editDate() !== inputValueOf(record.occurredAt)) {
      // Cambia solo il giorno: l'ora resta quella della nota.
      changes.occurredAt = toOccurredAt(this.editDate(), new Date(record.occurredAt));
    }
    const textChanged = record.text !== null && this.editText() !== record.text;
    if (textChanged) {
      if (!this.editText().trim()) {
        this.actionError.set('Il testo non può essere vuoto.');
        return;
      }
      changes.text = this.editText();
    }

    if (Object.keys(changes).length === 0) {
      this.editing.set(false);
      return;
    }

    this.busy.set(true);
    this.actionError.set(null);
    try {
      const updated = await firstValueFrom(this.api.update(record.recordUid, changes));
      this.store.replace(updated);
      if (textChanged) {
        // Il testo va indicizzato di nuovo: il record torna "in elaborazione" nella lista.
        void this.router.navigate(['/']);
        return;
      }
      this.record.set(updated);
      this.editing.set(false);
    } catch (err) {
      console.error('Modifica del record non riuscita', err);
      this.actionError.set(
        err instanceof HttpErrorResponse && err.status === 409
          ? 'La nota è stata aggiornata nel frattempo: ricarica la pagina e riprova.'
          : 'Salvataggio non riuscito.',
      );
    } finally {
      this.busy.set(false);
    }
  }

  protected confirmDelete(): void {
    this.actionError.set(null);
    this.deleteDialog().nativeElement.showModal();
  }

  protected async delete(record: RecordDetail): Promise<void> {
    this.busy.set(true);
    try {
      await firstValueFrom(this.api.delete(record.recordUid));
      this.store.remove(record.recordUid);
      this.deleteDialog().nativeElement.close();
      void this.router.navigate(['/']);
    } catch (err) {
      console.error('Eliminazione del record non riuscita', err);
      this.actionError.set('Eliminazione non riuscita.');
      this.deleteDialog().nativeElement.close();
    } finally {
      this.busy.set(false);
    }
  }
}
