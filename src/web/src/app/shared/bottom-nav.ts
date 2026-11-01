import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { NoteDraft } from '../notes/note-draft';
import { AUDIO_ACCEPT } from '../uploads/audio-formats';
import { TEXT_ACCEPT } from '../uploads/text-file';

/**
 * Barra di navigazione in basso, in tutte le pagine: Home, Aggiungi (al centro) e Tag, solo icone (con
 * etichetta accessibile). Aggiungi apre il menu con Registra, Scrivi e Carica; Carica apre la scelta dei file.
 */
@Component({
  selector: 'app-bottom-nav',
  imports: [RouterLink, RouterLinkActive, MatButtonModule, MatIconModule, MatMenuModule],
  template: `
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
      <button mat-icon-button aria-label="Aggiungi una nota" [matMenuTriggerFor]="addMenu">
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

    <mat-menu #addMenu yPosition="above">
      <a mat-menu-item routerLink="/new/record">
        <mat-icon svgIcon="mic" />
        <span>Registra</span>
      </a>
      <a mat-menu-item routerLink="/new/write">
        <mat-icon svgIcon="write" />
        <span>Scrivi</span>
      </a>
      <button mat-menu-item (click)="fileInput.click()">
        <mat-icon svgIcon="upload" />
        <span>Carica</span>
      </button>
    </mat-menu>

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
  `,
})
export class BottomNav {
  private readonly router = inject(Router);
  private readonly drafts = inject(NoteDraft);

  protected readonly uploadAccept = `${AUDIO_ACCEPT},${TEXT_ACCEPT}`;

  protected selectFile(input: HTMLInputElement): void {
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (files.length > 0) {
      this.drafts.set(files);
      void this.router.navigate(['/new', 'upload']);
    }
  }
}
