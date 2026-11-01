import { Component, input } from '@angular/core';

/**
 * Icona di Memoa dentro l'app (stesso disegno di favicon.svg), nel colore d'accento del tema: viola in
 * chiaro, lilla in scuro. Un'immagine esterna non vedrebbe i colori dell'app né il tema scelto nelle
 * impostazioni. Decorativa: nessun testo alternativo.
 */
@Component({
  selector: 'app-logo',
  template: `
    <svg viewBox="0 0 260 260" aria-hidden="true" [attr.width]="size()" [attr.height]="size()">
      <rect width="260" height="260" rx="56" fill="var(--mat-sys-primary)" />
      <rect x="71" y="53" width="118" height="153" rx="12" fill="#fff" />
      <g fill="var(--mat-sys-primary)">
        <rect x="85" y="111" width="11" height="37" rx="5.5" />
        <rect x="104.5" y="95" width="11" height="69" rx="5.5" />
        <rect x="124" y="80" width="11" height="99" rx="5.5" />
        <rect x="143.5" y="100" width="11" height="58" rx="5.5" />
        <rect x="163" y="113" width="11" height="32" rx="5.5" />
      </g>
    </svg>
  `,
  styles: `
    :host {
      display: block;
      line-height: 0;
    }
  `,
})
export class AppLogo {
  readonly size = input(32);
}
