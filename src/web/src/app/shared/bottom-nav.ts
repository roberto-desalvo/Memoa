import { Component, ElementRef, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AUDIO_ACCEPT } from '../uploads/audio-formats';
import { TEXT_ACCEPT } from '../uploads/text-file';
import { UploadQueue } from '../uploads/upload-queue';

/**
 * Barra di navigazione in basso, in tutte le pagine: Home, Aggiungi (al centro) e Tag, solo icone (con
 * etichetta accessibile). Aggiungi espande in orizzontale, sopra la barra, le azioni Scrivi, Registra e
 * Carica (icona con una piccola etichetta sotto); Carica apre la scelta dei file. La barra delle azioni si
 * chiude con un secondo tocco su Aggiungi, toccando fuori, con Esc o scegliendo un'azione.
 */
@Component({
  selector: 'app-bottom-nav',
  imports: [RouterLink, RouterLinkActive, MatButtonModule, MatIconModule],
  host: {
    '(document:click)': 'closeOnOutsideClick($event)',
    '(document:keydown.escape)': 'addOpen.set(false)',
  },
  template: `
    <div id="add-actions" class="add-actions" [class.open]="addOpen()" [inert]="!addOpen()">
      <a routerLink="/new/write" (click)="addOpen.set(false)">
        <mat-icon svgIcon="write" />
        <span>Scrivi</span>
      </a>
      <a routerLink="/new/record" (click)="addOpen.set(false)">
        <mat-icon svgIcon="mic" />
        <span>Registra</span>
      </a>
      <button type="button" (click)="addOpen.set(false); fileInput.click()">
        <mat-icon svgIcon="upload" />
        <span>Carica</span>
      </button>
    </div>

    <nav aria-label="Navigazione principale">
      <a
        mat-icon-button
        routerLink="/"
        routerLinkActive="active"
        ariaCurrentWhenActive="page"
        [routerLinkActiveOptions]="{ exact: true }"
        aria-label="Home"
      >
        <mat-icon svgIcon="home" />
      </a>
      <button
        mat-icon-button
        class="add"
        [class.active]="addOpen()"
        aria-label="Aggiungi una nota"
        aria-controls="add-actions"
        [attr.aria-expanded]="addOpen()"
        (click)="addOpen.set(!addOpen())"
      >
        <mat-icon svgIcon="add" />
      </button>
      <a
        mat-icon-button
        routerLink="/tags"
        routerLinkActive="active"
        ariaCurrentWhenActive="page"
        aria-label="Tag"
      >
        <mat-icon svgIcon="tag" />
      </a>
    </nav>

    <input
      #fileInput
      type="file"
      multiple
      [accept]="uploadAccept"
      (change)="selectFile(fileInput)"
    />
  `,
  styles: `
    // Fissa in fondo, a tutta larghezza, con il fondo delle liste e un bordo sottile sopra.
    :host {
      position: fixed;
      inset: auto 0 0;
      z-index: 20;
      padding-bottom: env(safe-area-inset-bottom);
      background: var(--mat-sys-surface-container-lowest);
      border-top: 1px solid var(--mat-sys-outline-variant);
    }

    nav {
      display: flex;
      justify-content: space-around;
      align-items: center;
      max-width: 44rem;
      height: var(--nav-height);
      margin: 0 auto;
      padding-inline: env(safe-area-inset-left) env(safe-area-inset-right);
    }

    // Area toccabile larga quanto l'indicatore della voce attiva (64 × 48 px).
    .mat-mdc-icon-button {
      --mat-icon-button-state-layer-size: 48px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: calc(2 * var(--space-6));
      padding: 0;
      border-radius: var(--space-5);
      color: var(--mat-sys-on-surface-variant);
    }

    // Voce attiva: icona nel colore d'accento su una tinta tenue (indicatore a pillola).
    .active {
      color: var(--mat-sys-primary);
      background: var(--accent-soft);
    }

    // Aperta, la croce di Aggiungi ruota e diventa una X.
    .add mat-icon {
      transition: transform 200ms ease;
    }

    .add.active mat-icon {
      transform: rotate(45deg);
    }

    // Pillola centrata sopra Aggiungi, che si espande in orizzontale dal centro: larga quasi quanto la
    // pagina su telefono (32 px di margine per lato), al massimo 28rem sugli schermi grandi.
    .add-actions {
      position: absolute;
      bottom: calc(100% + var(--space-2));
      left: 50%;
      display: flex;
      gap: var(--space-1);
      width: min(100% - 2 * var(--space-6), 28rem);
      padding: var(--space-1);
      background: var(--mat-sys-surface-container-lowest);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--space-5);
      box-shadow: var(--mat-sys-level2);
      opacity: 0;
      transform: translateX(-50%) scaleX(0.2);
      visibility: hidden;
      transition:
        transform 200ms ease,
        opacity 150ms ease,
        visibility 0s 200ms;

      &.open {
        opacity: 1;
        transform: translateX(-50%) scaleX(1);
        visibility: visible;
        transition:
          transform 200ms ease,
          opacity 150ms ease;
      }

      // Icona sopra, etichetta piccola sotto.
      a,
      button {
        display: flex;
        flex: 1;
        flex-direction: column;
        align-items: center;
        gap: 2px;
        min-width: calc(2 * var(--space-6));
        padding: var(--space-2) var(--space-3);
        border: 0;
        border-radius: var(--space-4);
        background: none;
        color: var(--mat-sys-on-surface-variant);
        font: var(--mat-sys-label-small);
        text-decoration: none;
        cursor: pointer;

        &:hover,
        &:focus-visible {
          color: var(--mat-sys-primary);
          background: var(--accent-soft);
          outline: none;
        }
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .add mat-icon,
      .add-actions,
      .add-actions.open {
        transition: none;
      }
    }
  `,
})
export class BottomNav {
  private readonly router = inject(Router);
  private readonly uploads = inject(UploadQueue);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly uploadAccept = `${AUDIO_ACCEPT},${TEXT_ACCEPT}`;
  protected readonly addOpen = signal(false);

  protected closeOnOutsideClick(event: MouseEvent): void {
    if (this.addOpen() && !this.host.nativeElement.contains(event.target as Node)) {
      this.addOpen.set(false);
    }
  }

  protected selectFile(input: HTMLInputElement): void {
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (files.length > 0) {
      this.uploads.add(files);
      void this.router.navigate(['/new', 'upload']);
    }
  }
}
