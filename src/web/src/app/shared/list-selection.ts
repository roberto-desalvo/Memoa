import { computed, signal } from '@angular/core';

/**
 * Selezione multipla e voce scorsa di una lista (homepage, pagina Tag). Le voci selezionate si
 * aggiungono con la pressione prolungata e si aggiungono o tolgono con un tocco; una voce che non è più
 * nella lista sparisce dalla selezione. Lo scorrimento che scopre il cestino apre una voce alla volta.
 */
export class ListSelection<T> {
  private readonly uids = signal<ReadonlySet<string>>(new Set());

  /** Voci selezionate, nell'ordine della lista. */
  readonly selected = computed(() =>
    this.items().filter((item) => this.uids().has(this.key(item))),
  );
  readonly selecting = computed(() => this.selected().length > 0);

  /** Voce scorsa verso destra, con il cestino scoperto. */
  readonly swiped = signal<string | null>(null);

  constructor(
    private readonly items: () => readonly T[],
    private readonly key: (item: T) => string,
  ) {}

  isSelected(item: T): boolean {
    return this.uids().has(this.key(item));
  }

  isSwiped(item: T): boolean {
    return this.swiped() === this.key(item);
  }

  setSwiped(item: T, open: boolean): void {
    this.swiped.set(open ? this.key(item) : null);
  }

  /** Aggiunge la voce alla selezione (pressione prolungata) e chiude la voce scorsa. */
  select(item: T): void {
    this.swiped.set(null);
    this.uids.update((uids) => new Set(uids).add(this.key(item)));
  }

  /** Tocco su una voce mentre la selezione è attiva: la aggiunge o la toglie. */
  toggle(item: T): void {
    this.uids.update((uids) => {
      const next = new Set(uids);
      if (!next.delete(this.key(item))) {
        next.add(this.key(item));
      }
      return next;
    });
  }

  clear(): void {
    this.uids.set(new Set());
  }

  /** Esc: chiude la selezione e la voce scorsa. */
  dismiss(): void {
    this.clear();
    this.swiped.set(null);
  }
}
