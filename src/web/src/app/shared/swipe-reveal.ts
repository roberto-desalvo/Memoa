import { computed, DestroyRef, Directive, ElementRef, inject, input, model, signal } from '@angular/core';

/** Larghezza dello spazio scoperto a sinistra dallo scorrimento (dove sta il cestino). */
export const SWIPE_REVEAL_PX = 72;
/** Spostamento oltre il quale la pressione diventa uno scorrimento, orizzontale o verticale. */
const MOVE_TOLERANCE_PX = 10;

/**
 * Scorrimento verso destra di una voce della lista: scopre a sinistra uno spazio largo
 * SWIPE_REVEAL_PX, dove l'elemento sotto mostra un'azione (il cestino in homepage). Rilasciata oltre metà
 * dello spazio la voce resta aperta, altrimenti torna al suo posto; lo scorrimento verticale resta della
 * pagina. Il clic che chiude lo scorrimento non apre la voce, e il tocco su una voce aperta la chiude.
 */
@Directive({
  selector: '[appSwipeReveal]',
  host: {
    '(pointerdown)': 'start($event)',
    '(pointermove)': 'move($event)',
    '(pointerup)': 'end()',
    '(pointercancel)': 'end()',
    // Con il mouse il browser trascinerebbe il collegamento.
    '(dragstart)': '$event.preventDefault()',
    '[class.swiping]': 'offset() !== null',
    '[style.transform]': 'transform()',
  },
})
export class SwipeReveal {
  /** Voce aperta, con l'azione scoperta. */
  readonly open = model(false);
  readonly swipeDisabled = input(false);

  /** Posizione durante lo scorrimento; null quando la voce sta ferma (chiusa o aperta). */
  protected readonly offset = signal<number | null>(null);
  protected readonly transform = computed(() => {
    const x = this.offset() ?? (this.open() ? SWIPE_REVEAL_PX : 0);
    return x > 0 ? `translateX(${x}px)` : '';
  });

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private origin?: { x: number; y: number; base: number; pointerId: number };
  private swiping = false;
  private swiped = false;

  constructor() {
    const onClick = (event: MouseEvent) => {
      if (this.swiped || this.open()) {
        event.preventDefault();
        event.stopPropagation();
        if (!this.swiped) {
          this.open.set(false);
        }
        this.swiped = false;
      }
    };
    this.element.addEventListener('click', onClick, true);
    inject(DestroyRef).onDestroy(() => this.element.removeEventListener('click', onClick, true));
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
    if (this.swiping && offset !== null) {
      this.open.set(offset >= SWIPE_REVEAL_PX / 2);
      this.swiped = true;
    }
    this.offset.set(null);
    this.origin = undefined;
    this.swiping = false;
  }
}
