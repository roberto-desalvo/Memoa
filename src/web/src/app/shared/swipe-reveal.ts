import { computed, DestroyRef, Directive, ElementRef, inject, input, model, signal } from '@angular/core';

/** Larghezza dello spazio scoperto a sinistra dallo scorrimento (dove sta il cestino). */
export const SWIPE_REVEAL_PX = 72;
/** Spostamento oltre il quale la pressione diventa uno scorrimento, orizzontale o verticale. */
const MOVE_TOLERANCE_PX = 10;

/**
 * Scorrimento verso destra di una voce della lista: sull'elemento della voce (il li, classe swipe-item),
 * che contiene la riga (classe swipe-row) e l'azione scoperta (marcata con data-swipe-action, il cestino
 * row-delete in homepage e nella pagina Tag); gli stili sono in styles.scss. La riga si sposta con la
 * variabile CSS --swipe-x. Da chiusa, rilasciata oltre metà dello spazio, la voce resta aperta; da aperta
 * basta uno scorrimento verso sinistra per chiuderla. Lo scorrimento verticale resta della pagina. La voce
 * aperta si chiude anche toccando la riga (che allora non si apre) o un punto qualsiasi fuori dalla voce;
 * il clic che chiude uno scorrimento non vale come tocco.
 */
@Directive({
  selector: '[appSwipeReveal]',
  host: {
    class: 'swipe-item',
    '(pointerdown)': 'start($event)',
    '(pointermove)': 'move($event)',
    '(pointerup)': 'end()',
    '(pointercancel)': 'end()',
    '(document:pointerdown)': 'closeOnOutside($event)',
    // Con il mouse il browser trascinerebbe il collegamento.
    '(dragstart)': '$event.preventDefault()',
    '[class.swiping]': 'offset() !== null',
    // Letta dallo scorrimento tra le pagine (page-slide.ts), che lascia il gesto alla riga aperta.
    '[class.swipe-open]': 'open()',
    '[style.--swipe-x]': 'position()',
  },
})
export class SwipeReveal {
  /** Voce aperta, con l'azione scoperta. */
  readonly open = model(false);
  readonly swipeDisabled = input(false);

  /** Posizione durante lo scorrimento; null quando la voce sta ferma (chiusa o aperta). */
  protected readonly offset = signal<number | null>(null);
  protected readonly position = computed(
    () => `${this.offset() ?? (this.open() ? SWIPE_REVEAL_PX : 0)}px`,
  );

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private origin?: { x: number; y: number; base: number; pointerId: number };
  private swiping = false;
  private swiped = false;

  constructor() {
    const onClick = (event: MouseEvent) => {
      if (this.swiped) {
        this.swiped = false;
        event.preventDefault();
        event.stopPropagation();
      } else if (this.open() && !(event.target as Element).closest('[data-swipe-action]')) {
        event.preventDefault();
        event.stopPropagation();
        this.open.set(false);
      }
    };
    this.element.addEventListener('click', onClick, true);
    inject(DestroyRef).onDestroy(() => this.element.removeEventListener('click', onClick, true));
  }

  protected closeOnOutside(event: PointerEvent): void {
    if (this.open() && !this.element.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  protected start(event: PointerEvent): void {
    this.swiped = false;
    if (this.swipeDisabled() || event.button !== 0) {
      return;
    }
    this.origin = {
      x: event.clientX,
      y: event.clientY,
      base: this.open() ? SWIPE_REVEAL_PX : 0,
      pointerId: event.pointerId,
    };
    this.swiping = false;
  }

  protected move(event: PointerEvent): void {
    if (!this.origin) {
      return;
    }
    const dx = event.clientX - this.origin.x;
    const dy = event.clientY - this.origin.y;
    if (!this.swiping) {
      if (Math.abs(dx) > MOVE_TOLERANCE_PX && Math.abs(dx) > Math.abs(dy)) {
        this.swiping = true;
        this.element.setPointerCapture(this.origin.pointerId);
      } else if (Math.abs(dy) > MOVE_TOLERANCE_PX) {
        this.origin = undefined;
        return;
      } else {
        return;
      }
    }
    // Oltre lo spazio dell'azione la voce segue il dito a metà velocità.
    const x = Math.max(0, this.origin.base + dx);
    this.offset.set(x <= SWIPE_REVEAL_PX ? x : SWIPE_REVEAL_PX + (x - SWIPE_REVEAL_PX) / 2);
  }

  protected end(): void {
    const offset = this.offset();
    if (this.swiping && offset !== null && this.origin) {
      this.open.set(
        this.origin.base > 0
          ? offset > SWIPE_REVEAL_PX - MOVE_TOLERANCE_PX
          : offset >= SWIPE_REVEAL_PX / 2,
      );
      this.swiped = true;
    }
    this.offset.set(null);
    this.origin = undefined;
    this.swiping = false;
  }
}
