import { inject } from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

/**
 * Icone dell'app, registrate come SVG inline e usate con `<mat-icon svgIcon="…" />`: nessun font di
 * icone scaricato da servizi esterni. Tutte con lo stesso tratto (24 × 24, linea di 2).
 */
const ICONS: Record<string, string> = {
  home: '<path d="M4 10.5 12 4l8 6.5V20h-5v-6h-6v6H4v-9.5Z" />',
  account: '<circle cx="12" cy="8" r="4" /><path d="M4 21a8 8 0 0 1 16 0" />',
  search: '<circle cx="11" cy="11" r="7" /><path d="m20 20-4-4" />',
  settings:
    '<path d="M4 6h8M18 6h2M4 12h2M12 12h8M4 18h10" /><circle cx="15" cy="6" r="3" />' +
    '<circle cx="9" cy="12" r="3" /><circle cx="17" cy="18" r="3" />',
  help: '<circle cx="12" cy="12" r="9" /><path d="M9.5 9.5a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6v.6M12 17v.01" />',
  info: '<circle cx="12" cy="12" r="9" /><path d="M12 11v5.5M12 7.5v.01" />',
  logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9" />',
  audio: '<path d="M4 10v4M8 7v10M12 4v16M16 8v8M20 10v4" />',
  text: '<path d="M5 7h14M5 12h14M5 17h9" />',
  mic: '<rect x="9" y="3" width="6" height="11" rx="3" /><path d="M5 11a7 7 0 0 0 14 0M12 18v3" />',
  stop: '<rect x="6" y="6" width="12" height="12" rx="2" fill="currentColor" />',
  pause:
    '<rect x="6" y="5" width="4" height="14" rx="1" fill="currentColor" />' +
    '<rect x="14" y="5" width="4" height="14" rx="1" fill="currentColor" />',
  resume: '<path d="M7 5v14l12-7-12-7Z" fill="currentColor" />',
  write: '<path d="M4 20h4L19 9l-4-4L4 16v4ZM13 7l4 4" />',
  upload: '<path d="M12 15V4M7 9l5-5 5 5M5 20h14" />',
  back: '<path d="M15 5l-7 7 7 7" />',
  up: '<path d="m6 15 6-6 6 6" />',
  down: '<path d="m6 9 6 6 6-6" />',
  close: '<path d="M6 6l12 12M18 6 6 18" />',
  add: '<path d="M12 5v14M5 12h14" />',
  more:
    '<circle cx="12" cy="5" r="1.5" fill="currentColor" /><circle cx="12" cy="12" r="1.5" fill="currentColor" />' +
    '<circle cx="12" cy="19" r="1.5" fill="currentColor" />',
  calendar: '<rect x="3" y="5" width="18" height="16" rx="2" /><path d="M3 10h18M8 3v4M16 3v4" />',
  check: '<path d="m5 12 5 5 9-10" />',
  pending: '<circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" />',
  todo: '<circle cx="12" cy="12" r="8" />',
  error: '<circle cx="12" cy="12" r="9" /><path d="M12 7.5v5M12 16.5v.01" />',
  delete: '<path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3" />',
  copy: '<rect x="9" y="9" width="12" height="12" rx="2" /><path d="M5 15H4a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v1" />',
  regenerate: '<path d="M20 12a8 8 0 1 1-2.34-5.66M20 4v5h-5" />',
  tag: '<path d="M3 12V4a1 1 0 0 1 1-1h8l9 9-9 9-9-9Z" /><circle cx="8" cy="8" r="1.5" fill="currentColor" />',
  filter: '<path d="M4 5h16l-6 7.5V19l-4 2v-8.5L4 5Z" />',
  auto: '<path d="M12 3v4M12 17v4M3 12h4M17 12h4M6.3 6.3l2.5 2.5M15.2 15.2l2.5 2.5M6.3 17.7l2.5-2.5M15.2 8.8l2.5-2.5" />',
};

export function registerIcons(): void {
  const registry = inject(MatIconRegistry);
  const sanitizer = inject(DomSanitizer);
  for (const [name, content] of Object.entries(ICONS)) {
    registry.addSvgIconLiteral(
      name,
      // Contenuto statico definito qui sopra, non proveniente dall'utente.
      sanitizer.bypassSecurityTrustHtml(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor"' +
          ` stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${content}</svg>`,
      ),
    );
  }
}
