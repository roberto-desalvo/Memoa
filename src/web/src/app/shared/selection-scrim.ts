import { Component, ElementRef, inject, output } from '@angular/core';

/**
 * Strato trasparente sopra la pagina mentre la selezione multipla è attiva (intestazione, filtri, barra
 * di navigazione): sopra restano solo le voci della lista, che si aggiungono o tolgono dalla selezione, e
 * l'intestazione della selezione. Toccandolo si chiude la selezione. Conta solo un tocco iniziato sullo
 * strato, non il rilascio della pressione che ha selezionato la voce.
 */
@Component({
  selector: 'app-selection-scrim',
  template: '',
  host: {
    class: 'selection-scrim',
    'aria-hidden': 'true',
    '(document:pointerdown)': 'armed = $event.target === element',
    '(click)': 'armed && dismiss.emit()',
    '(contextmenu)': '$event.preventDefault(); dismiss.emit()',
  },
})
export class SelectionScrim {
  readonly dismiss = output<void>();

  protected readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  protected armed = false;
}
