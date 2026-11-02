import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipListboxChange, MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { MsalService } from '@azure/msal-angular';
import { firstValueFrom } from 'rxjs';
import { ListGroup, listGroup, NoteDatePipe } from '../notes/note-date';
import { RecordsApi, RecordSummary, RecordTag } from '../records/records-api';
import { RecordsStore } from '../records/records-store';
import { NoteSearch } from '../search/note-search';
import { SearchBar } from '../search/search-bar';
import { AppLogo } from '../shared/app-logo';
import { deleteNotesDialog, openConfirmDialog } from '../shared/confirm-dialog';
import { LongPress } from '../shared/long-press';
import { NoteRow } from '../shared/note-row';
import { SwipeReveal } from '../shared/swipe-reveal';
import { TagFilter } from '../tags/tag-filter';
import { UploadQueue } from '../uploads/upload-queue';

@Component({
  selector: 'app-home',
  imports: [
    RouterLink,
    AppLogo,
    LongPress,
    NoteDatePipe,
    NoteRow,
    NoteSearch,
    SearchBar,
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
    '[class.selecting]': 'selecting()',
    '(document:keydown.escape)': 'clearSelection(); swiped.set(null)',
  },
})
export class Home implements OnInit, OnDestroy {
  private readonly msal = inject(MsalService);
  private readonly api = inject(RecordsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly store = inject(RecordsStore);
  protected readonly uploads = inject(UploadQueue);
  private readonly tagFilter = inject(TagFilter);

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

  /** Tag attivi assegnati ad almeno una nota, in ordine di nome: i filtri sotto la ricerca. */
  protected readonly tags = computed(() => recordTags(this.store.records() ?? []));

  /** Tag scelto tra i filtri, null per tutte le note (anche se il tag scelto non ha più note). */
  protected readonly selectedTag = computed(() => {
    const tagUid = this.tagFilter.tagUid();
    return this.tags().some((t) => t.tagUid === tagUid) ? tagUid : null;
  });

  /**
   * Record con il tag scelto raggruppati (oggi, ieri, poi per mese), nell'ordine della lista (dal più
   * recente).
   */
  protected readonly groups = computed(() => {
    const tagUid = this.selectedTag();
    const records = this.store.records() ?? [];
    return groupRecords(
      tagUid === null ? records : records.filter((r) => r.tags?.some((t) => t.tagUid === tagUid)),
    );
  });

  /**
   * Record selezionati: il primo con la pressione prolungata, gli altri con un tocco (o un'altra
   * pressione prolungata). Un record che non è più nella lista sparisce dalla selezione.
   */
  private readonly selectedUids = signal<ReadonlySet<string>>(new Set());
  protected readonly selected = computed(() =>
    (this.store.records() ?? []).filter((r) => this.selectedUids().has(r.recordUid)),
  );
  protected readonly selecting = computed(() => this.selected().length > 0);

  /** Nota scorsa verso destra, con il cestino scoperto: una alla volta. */
  protected readonly swiped = signal<string | null>(null);

  ngOnInit(): void {
    this.store.start();
  }

  ngOnDestroy(): void {
    this.store.stop();
  }

  /** Lo strato sopra la pagina chiude la selezione solo con un tocco iniziato su di esso. */
  protected scrimArmed = false;

  protected isSelected(record: RecordSummary): boolean {
    return this.selectedUids().has(record.recordUid);
  }

  protected isSwiped(record: RecordSummary): boolean {
    return this.swiped() === record.recordUid;
  }

  /** Cestino scoperto dallo scorrimento: chiede conferma; annullando la nota torna al suo posto. */
  protected async deleteSwiped(record: RecordSummary): Promise<void> {
    await this.delete([record]);
    this.swiped.set(null);
  }

  /** Aggiunge il record alla selezione (pressione prolungata). */
  protected select(record: RecordSummary): void {
    this.scrimArmed = false;
    this.swiped.set(null);
    this.selectedUids.update((uids) => new Set(uids).add(record.recordUid));
  }

  /** Tocco su un record mentre la selezione è attiva: lo aggiunge o lo toglie. */
  protected toggle(record: RecordSummary): void {
    this.selectedUids.update((uids) => {
      const next = new Set(uids);
      if (!next.delete(record.recordUid)) {
        next.add(record.recordUid);
      }
      return next;
    });
  }

  protected clearSelection(): void {
    this.selectedUids.set(new Set());
  }

  protected async deleteSelected(): Promise<void> {
    const records = this.selected();
    if (records.length > 0) {
      await this.delete(records);
    }
  }

  protected logout(): void {
    this.msal.logoutRedirect();
  }

  /** Toccando di nuovo il filtro scelto lo si toglie; "Tutte" resta scelto anche toccandolo di nuovo. */
  protected selectTag(change: MatChipListboxChange): void {
    const tagUid: string | null = change.value ?? null;
    this.tagFilter.tagUid.set(tagUid);
    if (tagUid === null) {
      change.source.value = null;
    }
  }

  /** Le note eliminate escono dalla lista e quindi dalla selezione; restano selezionate quelle non riuscite. */
  private async delete(records: RecordSummary[]): Promise<void> {
    if ((await openConfirmDialog(this.dialog, deleteNotesDialog(records.length))) !== 'confirm') {
      return;
    }
    const results = await Promise.allSettled(
      records.map((r) => firstValueFrom(this.api.delete(r.recordUid))),
    );
    let failed = 0;
    results.forEach((result, i) => {
      if (result.status === 'fulfilled') {
        this.store.remove(records[i].recordUid);
      } else {
        failed++;
        console.error('Eliminazione non riuscita', result.reason);
      }
    });
    if (failed > 0) {
      this.snackBar.open(
        failed === records.length
          ? 'Eliminazione non riuscita. Riprova più tardi.'
          : `Non è stato possibile eliminare ${failed === 1 ? '1 nota' : `${failed} note`} su ${records.length}. Riprova più tardi.`,
      );
    }
  }
}

/** Tag attivi delle note, ciascuno una volta, in ordine di nome. */
function recordTags(records: RecordSummary[]): RecordTag[] {
  const tags = new Map<string, RecordTag>();
  for (const tag of records.flatMap((r) => r.tags ?? []).filter((t) => t.isActive)) {
    tags.set(tag.tagUid, tag);
  }
  return [...tags.values()].sort((a, b) => a.name.localeCompare(b.name));
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
