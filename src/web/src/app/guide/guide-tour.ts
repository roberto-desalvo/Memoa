import { createGlobalPositionStrategy, createOverlayRef, OverlayRef } from '@angular/cdk/overlay';
import { ComponentPortal } from '@angular/cdk/portal';
import { inject, Injectable, Injector } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { NavigationStart, Router } from '@angular/router';
import { filter } from 'rxjs';
import { AccountState } from '../account/account-state';
import { MeApi } from '../account/me-api';
import { APP_CONFIG } from '../config/app-config';
import { openConfirmDialog } from '../shared/confirm-dialog';
import { GuideOverlay } from './guide-overlay';
import { guideSteps } from './guide-steps';

/**
 * Guida dell'app sulla homepage. La homepage la propone una volta per sessione finché l'utente non l'ha
 * completata né rifiutata; "Fine", "No, grazie", "Salta", la X ed Esc la chiudono per sempre (salvato
 * nell'account). Dal menu utente si può sempre rivedere. Uscendo dalla homepage durante il tour la guida
 * si chiude senza salvare: verrà proposta di nuovo al prossimo accesso.
 */
@Injectable({ providedIn: 'root' })
export class GuideTour {
  private readonly injector = inject(Injector);
  private readonly dialog = inject(MatDialog);
  private readonly api = inject(MeApi);
  private readonly account = inject(AccountState);
  private readonly steps = guideSteps(inject(APP_CONFIG).maxAudioDurationMinutes);

  private overlayRef: OverlayRef | null = null;
  private offered = false;

  constructor() {
    inject(Router)
      .events.pipe(filter((event) => event instanceof NavigationStart))
      .subscribe(() => this.close());
  }

  /** Proposta all'apertura della homepage, al più una volta per sessione. */
  async offer(): Promise<void> {
    if (this.offered || this.overlayRef) {
      return;
    }
    this.offered = true;
    const result = await openConfirmDialog(this.dialog, {
      title: 'Vuoi fare un giro di Memoa?',
      message:
        'Una breve guida ti mostra come aggiungere note, cercarle e organizzarle con i tag. Potrai ' +
        'rivederla quando vuoi dal menu utente, alla voce Guida.',
      confirm: 'Inizia il tour',
      cancel: 'No, grazie',
    });
    if (result === 'confirm') {
      this.start();
    } else {
      this.dismiss();
    }
  }

  start(): void {
    if (this.overlayRef) {
      return;
    }
    const overlayRef = createOverlayRef(this.injector, {
      positionStrategy: createGlobalPositionStrategy(this.injector),
      width: '100%',
      height: '100%',
    });
    const component = overlayRef.attach(new ComponentPortal(GuideOverlay));
    component.setInput('steps', this.steps);
    component.instance.closed.subscribe(() => {
      this.close();
      this.dismiss();
    });
    this.overlayRef = overlayRef;
  }

  private close(): void {
    this.overlayRef?.dispose();
    this.overlayRef = null;
  }

  /** Salva che la guida non va più proposta; rivista dal menu dopo averla chiusa non salva nulla. */
  private dismiss(): void {
    if (!this.account.me()?.showGuide) {
      return;
    }
    this.account.guideDismissed();
    this.api.dismissGuide().subscribe({
      // Al più verrà proposta di nuovo al prossimo accesso.
      error: (err) => console.error('Salvataggio della guida non riuscito', err),
    });
  }
}
