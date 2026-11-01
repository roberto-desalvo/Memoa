import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LIST_POLL_INTERVAL_MS } from '../config/polling';
import { TagsApi, TagSummary } from './tags-api';

/**
 * Elenco dei tag dell'utente, in ordine di nome: nome, descrizione, numero di note, stato
 * dell'assegnazione e interruttore per attivarli o disattivarli (un tag disattivato non viene più
 * assegnato in automatico). Il tap su un tag apre la sua pagina, dove si modificano nome e descrizione.
 * Mentre almeno un tag è in assegnazione la lista si aggiorna con il polling, come quella delle note.
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
export class TagsPage implements OnInit, OnDestroy {
  private readonly api = inject(TagsApi);
  private readonly snackBar = inject(MatSnackBar);

  /** Null finché non arriva la prima risposta. */
  protected readonly tags = signal<TagSummary[] | null>(null);
  protected readonly loadFailed = signal(false);
  /** Tag di cui si sta salvando l'interruttore. */
  protected readonly toggling = signal<ReadonlySet<string>>(new Set());

  private destroyed = false;
  private timer?: ReturnType<typeof setTimeout>;

  private readonly onVisibilityChange = () => {
    if (document.visibilityState === 'visible') {
      // Tornando sull'app l'assegnazione può essere finita nel frattempo.
      if (this.hasAssigning()) {
        void this.refresh();
      }
    } else {
      clearTimeout(this.timer);
    }
  };

  ngOnInit(): void {
    document.addEventListener('visibilitychange', this.onVisibilityChange);
    void this.load();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    clearTimeout(this.timer);
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
  }

  protected async load(): Promise<void> {
    this.loadFailed.set(false);
    try {
      this.show(await firstValueFrom(this.api.list()));
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
      // Riattivato: ora è in assegnazione.
      this.schedule();
    }
  }

  protected countLabel(count: number): string {
    return count === 1 ? '1 nota' : `${count} note`;
  }

  /** Aggiornamento del polling: un errore lascia la lista com'è e il tentativo successivo riprova. */
  private async refresh(): Promise<void> {
    clearTimeout(this.timer);
    try {
      const tags = await firstValueFrom(this.api.list());
      // Un interruttore in salvataggio verrebbe riportato indietro dalla risposta: si aspetta il prossimo giro.
      if (this.toggling().size === 0) {
        this.show(tags);
        return;
      }
    } catch (err) {
      console.error('Aggiornamento dei tag non riuscito', err);
    }
    this.schedule();
  }

  /** Mostra la lista, sostituendola solo se è cambiata, e pianifica il controllo successivo. */
  private show(tags: TagSummary[]): void {
    if (JSON.stringify(this.tags()) !== JSON.stringify(tags)) {
      this.tags.set(tags);
    }
    this.schedule();
  }

  private schedule(): void {
    clearTimeout(this.timer);
    if (!this.destroyed && document.visibilityState === 'visible' && this.hasAssigning()) {
      this.timer = setTimeout(() => void this.refresh(), LIST_POLL_INTERVAL_MS);
    }
  }

  private hasAssigning(): boolean {
    return this.tags()?.some((t) => t.status === 'Assigning') ?? false;
  }

  private replace(tag: TagSummary): void {
    this.tags.update((tags) => tags?.map((t) => (t.tagUid === tag.tagUid ? tag : t)) ?? null);
  }
}
