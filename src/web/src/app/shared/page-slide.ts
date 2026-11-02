import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, Router, ViewTransitionInfo } from '@angular/router';

/** Pagine principali affiancate, da sinistra a destra: Home e Tag. */
const SLIDE_PAGES = ['/', '/tags'];
/** Spostamento orizzontale minimo perché lo scorrimento cambi pagina. */
const SWIPE_MIN_PX = 60;

function pagePath(snapshot: ActivatedRouteSnapshot): string {
  let route = snapshot;
  while (route.firstChild) {
    route = route.firstChild;
  }
  return `/${route.routeConfig?.path ?? ''}`;
}

/**
 * Passaggio animato tra Home e Tag (View Transitions API, tramite withViewTransitions del router): la
 * pagina scorre lateralmente, verso sinistra andando ai Tag e verso destra tornando alla Home; la barra in
 * basso resta ferma (stili in styles.scss). Le altre navigazioni non hanno animazione. Nei browser senza
 * View Transitions la pagina cambia subito.
 */
export function slideBetweenPages({ transition, from, to }: ViewTransitionInfo): void {
  const fromIndex = SLIDE_PAGES.indexOf(pagePath(from));
  const toIndex = SLIDE_PAGES.indexOf(pagePath(to));
  if (fromIndex < 0 || toIndex < 0 || fromIndex === toIndex) {
    transition.skipTransition();
    return;
  }
  const root = document.documentElement;
  root.dataset['pageSlide'] = toIndex > fromIndex ? 'forward' : 'back';
  void transition.finished.finally(() => delete root.dataset['pageSlide']);
}

/**
 * Scorrimento col dito su Home e Tag per passare all'altra pagina: verso sinistra dalla Home ai Tag, verso
 * destra dai Tag alla Home. Usa gli eventi touch (passivi), che arrivano anche quando il browser gestisce
 * lo scorrimento verticale. Non vale con la selezione multipla attiva, sopra finestre e menu, nei campi e
 * nei cursori, e sulle righe delle liste quando il gesto è quello della riga: verso destra scopre il
 * cestino, verso sinistra chiude la riga già aperta.
 */
export function enablePageSwipe(): void {
  const router = inject(Router);
  let origin: { x: number; y: number; rowSwipe: boolean } | undefined;

  document.addEventListener(
    'touchstart',
    (event) => {
      origin = undefined;
      const target = event.target as Element;
      if (
        event.touches.length !== 1 ||
        !SLIDE_PAGES.includes(router.url.split(/[?#]/)[0]) ||
        document.querySelector('.selection-scrim') ||
        target.closest(
          '.cdk-overlay-container, input, textarea, select, [contenteditable], .mat-mdc-slider',
        )
      ) {
        return;
      }
      const row = target.closest('.swipe-item');
      const touch = event.touches[0];
      origin = { x: touch.clientX, y: touch.clientY, rowSwipe: row !== null };
      // La riga già aperta si chiude scorrendo in qualsiasi direzione: il gesto resta suo.
      if (row?.classList.contains('swipe-open')) {
        origin = undefined;
      }
    },
    { passive: true },
  );

  document.addEventListener(
    'touchend',
    (event) => {
      const start = origin;
      origin = undefined;
      const touch = event.changedTouches[0];
      if (!start || !touch) {
        return;
      }
      const dx = touch.clientX - start.x;
      const dy = touch.clientY - start.y;
      if (Math.abs(dx) < SWIPE_MIN_PX || Math.abs(dx) < 2 * Math.abs(dy)) {
        return;
      }
      // Verso destra, su una riga, il gesto scopre il cestino.
      if (dx > 0 && start.rowSwipe) {
        return;
      }
      const index = SLIDE_PAGES.indexOf(router.url.split(/[?#]/)[0]) + (dx < 0 ? 1 : -1);
      if (index >= 0 && index < SLIDE_PAGES.length) {
        void router.navigateByUrl(SLIDE_PAGES[index]);
      }
    },
    { passive: true },
  );

  document.addEventListener('touchcancel', () => (origin = undefined), { passive: true });
}
