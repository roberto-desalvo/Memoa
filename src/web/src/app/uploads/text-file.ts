import { MAX_TEXT_LENGTH } from '../records/records-api';

/** Dimensione massima di un file di testo caricato. */
export const MAX_TEXT_FILE_BYTES = 10 * 1024 * 1024;

/**
 * Per ora solo testo semplice, letto nel browser e salvato come testo scritto. DOCX e PDF richiedono
 * l'estrazione del testo, ancora da decidere.
 */
const TEXT_EXTENSIONS = ['txt', 'md'];

export const TEXT_ACCEPT = TEXT_EXTENSIONS.map((extension) => `.${extension}`).join(',');

export const TEXT_FORMATS_LABEL = 'TXT, MD';

export function isTextFile(file: File): boolean {
  const extension = file.name.split('.').pop()?.toLowerCase() ?? '';
  return TEXT_EXTENSIONS.includes(extension);
}

/** Testo del file (UTF-8), oppure il messaggio d'errore per l'utente. */
export async function readTextFile(file: File): Promise<{ text: string } | { error: string }> {
  if (file.size > MAX_TEXT_FILE_BYTES) {
    return { error: 'Il file supera il limite di 10 MB.' };
  }

  const text = await file.text();
  if (!text.trim()) {
    return { error: 'Il file è vuoto.' };
  }
  if (text.length > MAX_TEXT_LENGTH) {
    return { error: 'Il testo supera il limite di 50.000 caratteri.' };
  }
  return { text };
}
