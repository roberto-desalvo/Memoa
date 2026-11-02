import { computed, inject, Injectable, signal } from '@angular/core';
import { APP_CONFIG } from '../config/app-config';
import { dayKey, formatDayMonth } from '../notes/note-date';
import { RecordSummary, RecordTag } from '../records/records-api';
import { RecordsStore } from '../records/records-store';

export type FilterSourceType = 'Audio' | 'Text';
export type FilterPeriod = 'today' | 'last7' | 'thisMonth' | 'custom';

/** Filtro attivo mostrato come chip sotto la ricerca; la chiave serve a toglierlo con {@link NoteFilters.remove}. */
export interface FilterChip {
  key: string;
  label: string;
}

const SOURCE_TYPE_LABELS: Record<FilterSourceType, string> = { Audio: 'Audio', Text: 'Testo' };
export const PERIOD_LABELS: Record<Exclude<FilterPeriod, 'custom'>, string> = {
  today: 'Oggi',
  last7: 'Ultimi 7 giorni',
  thisMonth: 'Questo mese',
};

/**
 * Filtri della homepage (tipologia, tag, periodo, durata), validi per la lista e per i risultati della
 * ricerca. Restano in memoria tra una pagina e l'altra, come la ricerca: aprendo una nota e tornando
 * indietro i filtri sono ancora lì. Più tag valgono in OR. La durata riguarda solo gli audio: impostarla
 * sceglie la tipologia Audio; se l'utente poi la toglie, i testi tornano e la durata vale solo per gli audio.
 */
@Injectable({ providedIn: 'root' })
export class NoteFilters {
  private readonly store = inject(RecordsStore);
  /** Estremo destro dello slider della durata: al massimo, nessun limite superiore. */
  readonly maxDurationMinutes = inject(APP_CONFIG).maxAudioDurationMinutes;

  readonly sourceType = signal<FilterSourceType | null>(null);
  readonly tagUids = signal<string[]>([]);
  readonly period = signal<FilterPeriod | null>(null);
  /** Estremi del periodo personalizzato (yyyy-mm-dd, inclusi), vuoti se non impostati. */
  readonly from = signal('');
  readonly to = signal('');
  /** Durata in minuti, null senza limite. */
  readonly minMinutes = signal<number | null>(null);
  readonly maxMinutes = signal<number | null>(null);

  /** Tag attivi assegnati ad almeno una nota, in ordine di nome: i tag che si possono scegliere. */
  readonly tags = computed(() => recordTags(this.store.records() ?? []));

  /** Tag scelti che esistono ancora tra quelli delle note. */
  private readonly selectedTags = computed(() => {
    const uids = new Set(this.tagUids());
    return this.tags().filter((t) => uids.has(t.tagUid));
  });

  private readonly hasDuration = computed(
    () => this.minMinutes() !== null || this.maxMinutes() !== null,
  );

  readonly chips = computed<FilterChip[]>(() => {
    const chips: FilterChip[] = [];
    const sourceType = this.sourceType();
    if (sourceType) {
      chips.push({ key: 'type', label: SOURCE_TYPE_LABELS[sourceType] });
    }
    for (const tag of this.selectedTags()) {
      chips.push({ key: `tag:${tag.tagUid}`, label: tag.name });
    }
    const period = this.periodLabel();
    if (period) {
      chips.push({ key: 'period', label: period });
    }
    const duration = this.durationLabel();
    if (duration) {
      chips.push({ key: 'duration', label: duration });
    }
    return chips;
  });

  readonly active = computed(() => this.chips().length > 0);

  /** Vero se il record passa tutti i filtri attivi. */
  readonly matches = computed(() => {
    const sourceType = this.sourceType();
    const tagUids = new Set(this.selectedTags().map((t) => t.tagUid));
    const range = this.dayRange();
    const min = this.minMinutes();
    const max = this.maxMinutes();
    return (record: RecordSummary): boolean => {
      if (sourceType && record.sourceType !== sourceType) {
        return false;
      }
      if (tagUids.size > 0 && !record.tags?.some((t) => tagUids.has(t.tagUid))) {
        return false;
      }
      if (range) {
        const day = dayKey(new Date(record.occurredAt));
        if ((range.from && day < range.from) || (range.to && day > range.to)) {
          return false;
        }
      }
      if (record.sourceType === 'Audio' && (min !== null || max !== null)) {
        // Un audio non ancora normalizzato non ha la durata: con un limite non lo si può confrontare.
        const seconds = record.durationSeconds;
        if (
          seconds == null ||
          (min !== null && seconds < min * 60) ||
          (max !== null && seconds > max * 60)
        ) {
          return false;
        }
      }
      return true;
    };
  });

  /** Dallo slider: estremi 0 e massimo valgono come nessun limite. */
  setDuration(min: number, max: number): void {
    const hadDuration = this.hasDuration();
    this.minMinutes.set(min > 0 ? min : null);
    this.maxMinutes.set(max < this.maxDurationMinutes ? max : null);
    if (!hadDuration && this.hasDuration()) {
      this.sourceType.set('Audio');
    }
  }

  setPeriod(period: FilterPeriod | null): void {
    this.period.set(period);
    if (period !== 'custom') {
      this.from.set('');
      this.to.set('');
    }
  }

  remove(key: string): void {
    if (key === 'type') {
      this.sourceType.set(null);
    } else if (key === 'period') {
      this.setPeriod(null);
    } else if (key === 'duration') {
      this.minMinutes.set(null);
      this.maxMinutes.set(null);
    } else if (key.startsWith('tag:')) {
      const tagUid = key.slice(4);
      this.tagUids.update((uids) => uids.filter((uid) => uid !== tagUid));
    }
  }

  clear(): void {
    this.sourceType.set(null);
    this.tagUids.set([]);
    this.setPeriod(null);
    this.minMinutes.set(null);
    this.maxMinutes.set(null);
  }

  /** Giorni inclusi del periodo scelto, null senza filtro sul periodo. */
  private readonly dayRange = computed((): { from: string; to: string } | null => {
    const now = new Date();
    const today = dayKey(now);
    switch (this.period()) {
      case 'today':
        return { from: today, to: today };
      case 'last7':
        return {
          from: dayKey(new Date(now.getFullYear(), now.getMonth(), now.getDate() - 6)),
          to: today,
        };
      case 'thisMonth':
        return { from: `${today.slice(0, 7)}-01`, to: today };
      case 'custom': {
        const [from, to] = [this.from(), this.to()];
        return from || to ? { from, to } : null;
      }
      default:
        return null;
    }
  });

  private periodLabel(): string | null {
    const period = this.period();
    if (period === null) {
      return null;
    }
    if (period !== 'custom') {
      return PERIOD_LABELS[period];
    }
    const [from, to] = [this.from(), this.to()];
    if (from && to) {
      return `${formatDayMonth(from)} – ${formatDayMonth(to)}`;
    }
    if (from) {
      return `Dal ${formatDayMonth(from)}`;
    }
    return to ? `Fino al ${formatDayMonth(to)}` : null;
  }

  private durationLabel(): string | null {
    const [min, max] = [this.minMinutes(), this.maxMinutes()];
    if (min !== null && max !== null) {
      return `${min}–${max} min`;
    }
    if (min !== null) {
      return `Almeno ${min} min`;
    }
    return max !== null ? `Fino a ${max} min` : null;
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
