import { DestroyRef, Directive, ElementRef, inject, output } from '@angular/core';

/** Durata della pressione che seleziona la voce. */
const LONG_PRESS_MS = 500;
/** Spostamento oltre il quale la pressione diventa uno scorrimento e non conta più. */
const MOVE_TOLERANCE_PX = 10;

/**
 * Pressione prolungata su una voce della lista (o clic destro con il mouse). Il clic che segue la
 * pressione non apre la voce: si intercetta in fase di cattura, prima di RouterLink.
 */
@Directive({
  selector: '[appLongPress]',
  host: {
    '(pointerdown)': 'start($event)',
    '(pointermove)': 'move($event)',
    '(pointerup)': 'cancel()',
    '(pointercancel)': 'cancel()',
    '(pointerleave)': 'cancel()',
    // Su Android la pressione su un collegamento apre il menu del browser: lo sostituisce la selezione.
    '(contextmenu)': '$event.preventDefault(); fire()',
  },
})
export class LongPress {
  readonly longPress = output<void>();

  private timer?: ReturnType<typeof setTimeout>;
  private origin?: { x: number; y: number };
  private fired = false;

  constructor() {
    const element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const onClick = (event: MouseEvent) => {
      if (this.fired) {
        this.fired = false;
        event.preventDefault();
        event.stopPropagation();
      }
    };
    element.addEventListener('click', onClick, true);
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.timer);
      element.removeEventListener('click', onClick, true);
    });
  }

  protected start(event: PointerEvent): void {
    // Un clic destro non è seguito da un clic: la nuova pressione azzera lo stato della precedente.
    this.fired = false;
    if (event.button !== 0) {
      return;
    }
    this.origin = { x: event.clientX, y: event.clientY };
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.fire(), LONG_PRESS_MS);
  }

  protected move(event: PointerEvent): void {
    if (
      this.origin &&
      Math.hypot(event.clientX - this.origin.x, event.clientY - this.origin.y) > MOVE_TOLERANCE_PX
    ) {
      this.cancel();
    }
  }

  protected cancel(): void {
    clearTimeout(this.timer);
    this.origin = undefined;
  }

  protected fire(): void {
    this.cancel();
    this.fired = true;
    this.longPress.emit();
  }
}
