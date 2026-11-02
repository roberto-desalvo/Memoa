import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { MsalService } from '@azure/msal-angular';
import { AccountState } from '../account/account-state';
import { Me } from '../account/me-api';
import { GuideTour } from '../guide/guide-tour';
import { ListGroup, listGroup, NoteDatePipe } from '../notes/note-date';
import { RecordsApi, RecordSummary } from '../records/records-api';
import { RecordsStore } from '../records/records-store';
import { NoteSearch } from '../search/note-search';
import { SearchBar } from '../search/search-bar';
import { AppLogo } from '../shared/app-logo';
import { deleteNotesDialog, openConfirmDialog } from '../shared/confirm-dialog';
import { deleteItems } from '../shared/delete-items';
import { ListSelection } from '../shared/list-selection';
import { LongPress } from '../shared/long-press';
import { NoteRow } from '../shared/note-row';
import { SelectionScrim } from '../shared/selection-scrim';
import { SwipeReveal } from '../shared/swipe-reveal';
import { openRecordTagsDialog } from '../tags/record-tags-dialog';
import { UploadQueue } from '../uploads/upload-queue';
import { NoteFilters } from './note-filters';
import { NoteFiltersPanel } from './note-filters-panel';

@Component({
  selector: 'app-home',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    AppLogo,
    LongPress,
    NoteDatePipe,
    NoteFiltersPanel,
    NoteRow,
    NoteSearch,
    SearchBar,
    SelectionScrim,
    SwipeReveal,
    MatButtonModule,
    MatChipsModule,
    MatDividerModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatToolbarModule,
  ],
  templateUrl: './home.html',
  styleUrl: './home.scss',
  host: {
    class: 'page',
    '[class.selecting]': 'selection.selecting()',
    '(document:keydown.escape)': 'selection.dismiss()',
  },
})
export class Home implements OnInit, OnDestroy {
  private readonly msal = inject(MsalService);
  private readonly account = inject(AccountState);
  protected readonly guide = inject(GuideTour);
  private readonly api = inject(RecordsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly store = inject(RecordsStore);
  protected readonly uploads = inject(UploadQueue);
  protected readonly filters = inject(NoteFilters);

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

  /** Pannello dei filtri aperto dall'icona sopra la lista. */
  protected readonly filtersOpen = signal(false);

  /**
   * Record che passano i filtri raggruppati (oggi, ieri, poi per mese), nell'ordine della lista (dal più
   * recente).
   */
  protected readonly groups = computed(() =>
    groupRecords((this.store.records() ?? []).filter(this.filters.matches())),
  );

  /** Note selezionate con la pressione prolungata e nota scorsa verso destra, con il cestino scoperto. */
  protected readonly selection = new ListSelection(
    () => this.store.records() ?? [],
    (r: RecordSummary) => r.recordUid,
  );

  /** `/me` non riuscita: la lista non si carica finché l'utente non risulta registrato. */
  protected readonly registerFailed = signal(false);

  /** "Guida" scelta dal menu utente: parte alla chiusura del menu, dopo che il focus è tornato all'avatar. */
  private guideRequested = false;
  private destroyed = false;

  constructor() {
    // Al primo accesso della sessione, e di nuovo se una chiamata risponde che l'utente non è registrato
    // (AccountState).
    effect(() => {
      if (this.account.me() === null) {
        void this.register();
      }
    });
  }

  ngOnInit(): void {
    // Utente già registrato in questa sessione: la lista parte subito, senza chiamare /me.
    if (this.account.me() !== null) {
      this.store.start();
    }
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.store.stop();
  }

  protected retry(): void {
    if (this.registerFailed()) {
      void this.register();
    } else {
      void this.store.refresh();
    }
  }

  protected requestGuide(): void {
    this.guideRequested = true;
  }

  protected onUserMenuClosed(): void {
    if (this.guideRequested) {
      this.guideRequested = false;
      this.guide.start();
    }
  }

  private async register(): Promise<void> {
    this.registerFailed.set(false);
    let me: Me;
    try {
      me = await this.account.ensureRegistered();
    } catch (err) {
      console.error('Registrazione dell’utente non riuscita', err);
      this.registerFailed.set(true);
      return;
    }
    if (this.destroyed) {
      return;
    }
    // Con la homepage già aperta start() non ricarica: refresh() aggiorna subito (una sola richiesta).
    this.store.start();
    void this.store.refresh();
    if (me.showGuide) {
      void this.guide.offer();
    }
  }

  /** Cestino scoperto dallo scorrimento: chiede conferma; annullando la nota torna al suo posto. */
  protected async deleteSwiped(record: RecordSummary): Promise<void> {
    await this.delete([record]);
    this.selection.swiped.set(null);
  }

  protected async deleteSelected(): Promise<void> {
    const records = this.selection.selected();
    if (records.length > 0) {
      await this.delete(records);
    }
  }

  /** Tag delle note selezionate; dopo il salvataggio la selezione si chiude. */
  protected async editSelectedTags(): Promise<void> {
    const records = this.selection.selected();
    if (records.length > 0 && (await openRecordTagsDialog(this.dialog, records))) {
      this.selection.clear();
    }
  }

  protected logout(): void {
    this.msal.logoutRedirect();
  }

  /** Le note eliminate escono dalla lista e quindi dalla selezione; restano selezionate quelle non riuscite. */
  private async delete(records: RecordSummary[]): Promise<void> {
    if ((await openConfirmDialog(this.dialog, deleteNotesDialog(records.length))) !== 'confirm') {
      return;
    }
    const deleted = await deleteItems(
      records,
      (r) => this.api.delete(r.recordUid),
      this.snackBar,
      (count) => (count === 1 ? '1 nota' : `${count} note`),
    );
    deleted.forEach((r) => this.store.remove(r.recordUid));
  }
}

interface RecordGroup extends ListGroup {
  records: RecordSummary[];
}

/**
 * Un gruppo per oggi, per ieri e per ogni mese, nell'ordine in cui compare nella lista. Raggruppa per
 * chiave e non per record consecutivi: un record con la data appena modificata non crea un gruppo doppio.
 */
function groupRecords(records: RecordSummary[]): RecordGroup[] {
  const now = new Date();
  const groups = new Map<string, RecordGroup>();
  for (const record of records) {
    const group = listGroup(new Date(record.occurredAt), now);
    let entry = groups.get(group.key);
    if (!entry) {
      entry = { ...group, records: [] };
      groups.set(group.key, entry);
    }
    entry.records.push(record);
  }
  return [...groups.values()];
}
