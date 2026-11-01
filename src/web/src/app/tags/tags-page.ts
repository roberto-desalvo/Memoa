import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { TagsApi, TagSummary } from './tags-api';

/**
 * Elenco dei tag dell'utente, in ordine di nome: nome, descrizione, numero di note e interruttore per
 * attivarli o disattivarli (un tag disattivato non viene più assegnato in automatico). Il tap su un tag
 * apre la sua pagina, dove si modificano nome e descrizione.
 */
@Component({
  selector: 'app-tags-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatToolbarModule,
  ],
  templateUrl: './tags-page.html',
  styleUrl: './tags-page.scss',
  host: { class: 'page' },
})
export class TagsPage implements OnInit {
  private readonly api = inject(TagsApi);
  private readonly snackBar = inject(MatSnackBar);

  /** Null finché non arriva la prima risposta. */
  protected readonly tags = signal<TagSummary[] | null>(null);
  protected readonly loadFailed = signal(false);
  /** Tag di cui si sta salvando l'interruttore. */
  protected readonly toggling = signal<ReadonlySet<string>>(new Set());

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loadFailed.set(false);
    try {
      this.tags.set(await firstValueFrom(this.api.list()));
    } catch (err) {
      console.error('Caricamento dei tag non riuscito', err);
      this.loadFailed.set(true);
    }
  }

  protected async setActive(tag: TagSummary, isActive: boolean): Promise<void> {
    this.toggling.update((uids) => new Set(uids).add(tag.tagUid));
    this.replace({ ...tag, isActive });
    try {
      this.replace(await firstValueFrom(this.api.update(tag.tagUid, { isActive })));
    } catch (err) {
      console.error('Modifica del tag non riuscita', err);
      this.replace(tag);
      this.snackBar.open(isActive ? 'Attivazione non riuscita.' : 'Disattivazione non riuscita.');
    } finally {
      this.toggling.update((uids) => {
        const next = new Set(uids);
        next.delete(tag.tagUid);
        return next;
      });
    }
  }

  protected countLabel(count: number): string {
    return count === 1 ? '1 nota' : `${count} note`;
  }

  private replace(tag: TagSummary): void {
    this.tags.update((tags) => tags?.map((t) => (t.tagUid === tag.tagUid ? tag : t)) ?? null);
  }
}
