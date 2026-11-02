import { A11yModule } from '@angular/cdk/a11y';
import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { GuideStep } from './guide-steps';

/** Margine dai bordi dello schermo, come il margine laterale delle pagine su telefono. */
const GUTTER = 16;
/** Spazio tra l'elemento evidenziato e il bordo dell'evidenziazione, e tra questa e il fumetto. */
const SPOTLIGHT_PADDING = 4;
const BUBBLE_GAP = 12;
const BUBBLE_MAX_WIDTH = 352;

interface Box {
  top: number;
  left: number;
  width: number;
  height: number;
}

/**
 * Tour della guida, a schermo intero sopra l'app (in un overlay di {@link GuideTour}): oscura la pagina
 * lasciando scoperto l'elemento del passo e mostra accanto un fumetto con titolo, testo, avanzamento e i
 * pulsanti Salta, Indietro e Avanti (Fine all'ultimo passo). Esc chiude come Salta, le frecce cambiano
 * passo. La pagina sotto non si può toccare durante il tour.
 */
@Component({
  selector: 'app-guide-overlay',
  imports: [A11yModule, MatButtonModule, MatIconModule],
  host: {
    '(keydown)': 'onKeydown($event)',
    '(window:resize)': 'measure()',
    '(window:scroll)': 'measure()',
  },
  template: `
    @if (spotlight(); as box) {
      <div
        class="spotlight"
        aria-hidden="true"
        [style.top.px]="box.top"
        [style.left.px]="box.left"
        [style.width.px]="box.width"
        [style.height.px]="box.height"
      ></div>
    } @else {
      <div class="scrim" aria-hidden="true"></div>
    }

    <section
      #bubble
      class="bubble"
      role="dialog"
      aria-modal="true"
      aria-labelledby="guide-title"
      aria-describedby="guide-text"
      cdkTrapFocus
      cdkTrapFocusAutoCapture
      [style.top.px]="bubblePosition().top"
      [style.left.px]="bubblePosition().left"
      [style.width.px]="bubblePosition().width"
    >
      <header>
        <span class="text-label muted">{{ index() + 1 }} di {{ steps().length }}</span>
        <button mat-icon-button type="button" aria-label="Chiudi la guida" (click)="closed.emit()">
          <mat-icon svgIcon="close" />
        </button>
      </header>
      <div aria-live="polite">
        <h2 id="guide-title" class="text-title">{{ step().title }}</h2>
        <p id="guide-text" class="text-body">{{ step().text }}</p>
      </div>
      <div class="actions">
        @if (!last()) {
          <button mat-button type="button" class="skip" (click)="closed.emit()">Salta</button>
        }
        @if (index() > 0) {
          <button mat-button type="button" (click)="go(index() - 1)">Indietro</button>
        }
        <button #primary mat-flat-button type="button" (click)="next()">
          {{ last() ? 'Fine' : 'Avanti' }}
        </button>
      </div>
    </section>
  `,
  styles: `
    :host {
      position: fixed;
      inset: 0;
      display: block;
    }

    // Fuori dall'evidenziazione la pagina è oscurata dall'ombra del riquadro stesso.
    .spotlight {
      position: fixed;
      border-radius: var(--space-3);
      box-shadow: 0 0 0 100vmax rgb(0 0 0 / 0.55);
      transition:
        top 200ms ease,
        left 200ms ease,
        width 200ms ease,
        height 200ms ease;
    }

    .scrim {
      position: fixed;
      inset: 0;
      background: rgb(0 0 0 / 0.55);
    }

    .bubble {
      position: fixed;
      display: flex;
      flex-direction: column;
      gap: var(--space-2);
      padding: var(--space-2) var(--space-4) var(--space-3);
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface);
      border-radius: var(--space-4);
      box-shadow: var(--mat-sys-level3);
      transition:
        top 200ms ease,
        left 200ms ease;

      h2,
      p {
        margin: 0;
      }

      p {
        margin-top: var(--space-2);
      }
    }

    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-inline-end: calc(-1 * var(--space-2));
    }

    .actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--space-2);
    }

    .skip {
      margin-inline-end: auto;
    }

    @media (prefers-reduced-motion: reduce) {
      .spotlight,
      .bubble {
        transition: none;
      }
    }
  `,
})
export class GuideOverlay {
  private readonly injector = inject(Injector);

  readonly steps = input.required<GuideStep[]>();
  /** Guida completata o chiusa prima della fine: in entrambi i casi non va più proposta. */
  readonly closed = output<void>();

  protected readonly index = signal(0);
  protected readonly step = computed(() => this.steps()[this.index()]);
  protected readonly last = computed(() => this.index() === this.steps().length - 1);

  /** Riquadro dell'elemento evidenziato, entro lo schermo; null se il passo non ne ha uno visibile. */
  protected readonly spotlight = signal<Box | null>(null);

  private readonly viewport = signal({ width: window.innerWidth, height: window.innerHeight });
  private readonly bubbleHeight = signal(0);
  private readonly bubble = viewChild.required<ElementRef<HTMLElement>>('bubble');
  private readonly primary = viewChild.required<string, ElementRef<HTMLElement>>('primary', {
    read: ElementRef,
  });

  /**
   * Sotto l'elemento se c'è spazio, altrimenti sopra; se non c'è spazio da nessuna parte (elemento molto
   * alto, come la lista) in basso sullo schermo. Senza elemento al centro. Su telefono il fumetto è largo
   * quanto lo schermo meno i margini.
   */
  protected readonly bubblePosition = computed(() => {
    const { width: screenWidth, height: screenHeight } = this.viewport();
    const height = this.bubbleHeight();
    const width = Math.min(screenWidth - 2 * GUTTER, BUBBLE_MAX_WIDTH);
    const box = this.spotlight();

    if (!box) {
      return {
        top: Math.max(GUTTER, (screenHeight - height) / 2),
        left: (screenWidth - width) / 2,
        width,
      };
    }

    const left = clamp(box.left + box.width / 2 - width / 2, GUTTER, screenWidth - GUTTER - width);
    const below = box.top + box.height + BUBBLE_GAP;
    const above = box.top - BUBBLE_GAP - height;
    const top =
      below + height <= screenHeight - GUTTER
        ? below
        : above >= GUTTER
          ? above
          : Math.max(GUTTER, screenHeight - GUTTER - height);
    return { top, left, width };
  });

  constructor() {
    // Altezza reale del fumetto, che cambia con il testo del passo e la larghezza dello schermo.
    afterNextRender(() => {
      const observer = new ResizeObserver(([entry]) =>
        this.bubbleHeight.set(entry.borderBoxSize[0].blockSize),
      );
      observer.observe(this.bubble().nativeElement);
      this.injector.get(DestroyRef).onDestroy(() => observer.disconnect());
    });

    // A ogni passo porta l'elemento sullo schermo e lo misura.
    effect(() => {
      const anchor = this.step().anchor;
      afterNextRender(
        () => {
          anchorElement(anchor)?.scrollIntoView({ block: 'nearest' });
          this.measure();
        },
        { injector: this.injector },
      );
    });
  }

  protected measure(): void {
    const width = window.innerWidth;
    const height = window.innerHeight;
    this.viewport.set({ width, height });

    const element = anchorElement(this.step().anchor);
    if (!element) {
      this.spotlight.set(null);
      return;
    }
    const rect = element.getBoundingClientRect();
    const top = Math.max(rect.top - SPOTLIGHT_PADDING, 0);
    const left = Math.max(rect.left - SPOTLIGHT_PADDING, 0);
    const bottom = Math.min(rect.bottom + SPOTLIGHT_PADDING, height);
    const right = Math.min(rect.right + SPOTLIGHT_PADDING, width);
    this.spotlight.set(
      bottom > top && right > left
        ? { top, left, width: right - left, height: bottom - top }
        : null,
    );
  }

  protected next(): void {
    if (this.last()) {
      this.closed.emit();
    } else {
      this.go(this.index() + 1);
    }
  }

  protected go(index: number): void {
    this.index.set(index);
    // Il pulsante premuto può sparire (Indietro al primo passo): il focus torna su Avanti.
    afterNextRender(
      () => {
        if (!this.bubble().nativeElement.contains(document.activeElement)) {
          this.primary().nativeElement.focus();
        }
      },
      { injector: this.injector },
    );
  }

  protected onKeydown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'Escape':
        event.preventDefault();
        this.closed.emit();
        break;
      case 'ArrowRight':
        event.preventDefault();
        this.next();
        break;
      case 'ArrowLeft':
        event.preventDefault();
        if (this.index() > 0) {
          this.go(this.index() - 1);
        }
        break;
    }
  }
}

/** Elemento visibile con l'attributo `data-guide` del passo. */
function anchorElement(anchor: string | null): HTMLElement | null {
  if (!anchor) {
    return null;
  }
  const element = document.querySelector<HTMLElement>(`[data-guide="${anchor}"]`);
  return element && element.getClientRects().length > 0 ? element : null;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}
