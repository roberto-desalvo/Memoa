/**
 * File di testo caricati: circa due pagine. Il file va su Blob Storage come l'audio e il testo lo estrae
 * il backend, che ricontrolla formato reale e limiti (DocumentLimits nelle Functions).
 */
export const MAX_TEXT_FILE_BYTES = 16 * 1024;
export const MAX_PDF_BYTES = 1024 * 1024;

/** Caratteri del testo estratto da un file caricato (le note scritte hanno MAX_TEXT_LENGTH). */
export const MAX_FILE_TEXT_LENGTH = 8_000;

/** Tipo MIME per estensione, salvato sul blob: decide il limite e l'estensione del file scaricato. */
const TEXT_TYPES: Record<string, string> = {
  txt: 'text/plain',
  md: 'text/markdown',
  pdf: 'application/pdf',
};

export const TEXT_ACCEPT = Object.keys(TEXT_TYPES)
  .map((extension) => `.${extension}`)
  .join(',');

export const TEXT_FORMATS_LABEL = 'TXT, MD, PDF';

/** Limiti per l'utente, ad es. "TXT e MD fino a 16 KB, PDF fino a 1 MB". */
export const TEXT_LIMITS_LABEL = `TXT e MD fino a ${MAX_TEXT_FILE_BYTES / 1024} KB, PDF fino a ${MAX_PDF_BYTES / (1024 * 1024)} MB`;

/** Tipo MIME del file di testo, oppure null se non è un formato di testo ammesso. */
export function textContentType(file: File): string | null {
  const extension = file.name.split('.').pop()?.toLowerCase() ?? '';
  return TEXT_TYPES[extension] ?? null;
}

export function isTextFile(file: File): boolean {
  return textContentType(file) !== null;
}

/**
 * Controlli prima dell'upload: dimensione e, per TXT e MD (piccoli), testo vuoto o troppo lungo.
 * Restituisce il messaggio d'errore per l'utente, oppure null.
 */
export async function checkTextFile(file: File, contentType: string): Promise<string | null> {
  if (file.size === 0) {
    return 'Il file è vuoto.';
  }

  if (contentType === 'application/pdf') {
    return file.size > MAX_PDF_BYTES ? 'Il PDF supera il limite di 1 MB.' : null;
  }

  if (file.size > MAX_TEXT_FILE_BYTES) {
    return `Il file supera il limite di ${MAX_TEXT_FILE_BYTES / 1024} KB.`;
  }

  const text = await file.text();
  if (!text.trim()) {
    return 'Il file è vuoto.';
  }
  if (text.length > MAX_FILE_TEXT_LENGTH) {
    return `Il testo supera il limite di ${MAX_FILE_TEXT_LENGTH.toLocaleString('it-IT')} caratteri (circa due pagine).`;
  }
  return null;
}
