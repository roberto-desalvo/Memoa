import { Component, inject, input, OnInit, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NoteDatePipe } from '../notes/note-date';
import { openConfirmDialog } from '../shared/confirm-dialog';
import { NoteRow } from '../shared/note-row';
import {
  MAX_TAG_DESCRIPTION_LENGTH,
  MAX_TAG_NAME_LENGTH,
  TagChanges,
  TagDetail,
  TagsApi,
  tagSaveError,
} from './tags-api';

/**
 * Pagina di un tag: nome e descrizione modificabili, eliminazione e note a cui il tag è assegnato.
 * Senza tagUid (percorso /tags/new) crea un tag nuovo. Dopo la creazione o una modifica di nome o
 * descrizione il tag viene applicato alle note esistenti in background.
 */
@Component({
  selector: 'app-tag-page',
  imports: [
    RouterLink,
    NoteDatePipe,
    NoteRow,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatToolbarModule,
  ],
  templateUrl: './tag-page.html',
  styleUrl: './tag-page.scss',
  host: { class: 'page' },
})
export class TagPage implements OnInit {
  private readonly api = inject(TagsApi);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  /** Dal percorso /tags/:tagUid; assente in /tags/new. */
  readonly tagUid = input<string>();

  protected readonly tag = signal<TagDetail | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected readonly maxNameLength = MAX_TAG_NAME_LENGTH;
  protected readonly maxDescriptionLength = MAX_TAG_DESCRIPTION_LENGTH;

  ngOnInit(): void {
    if (this.tagUid()) {
      void this.load();
    }
  }

  protected async load(): Promise<void> {
    this.loadError.set(null);
    try {
      const tag = await firstValueFrom(this.api.get(this.tagUid()!));
      this.tag.set(tag);
      this.name.set(tag.name);
      this.description.set(tag.description ?? '');
    } catch (err) {
      console.error('Caricamento del tag non riuscito', err);
      this.loadError.set(
        err instanceof HttpErrorResponse && err.status === 404
          ? 'Tag non trovato.'
          : 'Impossibile caricare il tag.',
      );
    }
  }

  protected async save(): Promise<void> {
    const name = this.name().trim();
    if (!name) {
      this.actionError.set('Il nome è obbligatorio.');
      return;
    }
    const description = this.description().trim();

    this.busy.set(true);
    this.actionError.set(null);
    try {
      const tag = this.tag();
      if (!tag) {
        const created = await firstValueFrom(
          this.api.create({ name, description: description || undefined }),
        );
        this.snackBar.open('Tag creato. Le note esistenti vengono aggiornate a breve.');
        void this.router.navigate(['/tags', created.tagUid], { replaceUrl: true });
        return;
      }

      const changes: TagChanges = {};
      if (name !== tag.name) {
        changes.name = name;
      }
      if (description !== (tag.description ?? '')) {
        changes.description = description;
      }
      if (Object.keys(changes).length === 0) {
        return;
      }

      const updated = await firstValueFrom(this.api.update(tag.tagUid, changes));
      this.tag.set({ ...tag, ...updated });
      this.name.set(updated.name);
      this.description.set(updated.description ?? '');
      this.snackBar.open(
        updated.isActive ? 'Salvato. Le note vengono aggiornate a breve.' : 'Salvato.',
      );
    } catch (err) {
      console.error('Salvataggio del tag non riuscito', err);
      this.actionError.set(tagSaveError(err));
    } finally {
      this.busy.set(false);
    }
  }

  protected async confirmDelete(): Promise<void> {
    const tag = this.tag();
    if (!tag) {
      return;
    }
    const result = await openConfirmDialog(this.dialog, {
      title: 'Eliminare il tag?',
      message: 'Il tag viene tolto da tutte le note. Le note non vengono eliminate.',
      confirm: 'Elimina',
    });
    if (result !== 'confirm') {
      return;
    }

    this.busy.set(true);
    try {
      await firstValueFrom(this.api.delete(tag.tagUid));
      void this.router.navigate(['/tags']);
    } catch (err) {
      console.error('Eliminazione del tag non riuscita', err);
      this.snackBar.open('Eliminazione non riuscita.');
    } finally {
      this.busy.set(false);
    }
  }
}
