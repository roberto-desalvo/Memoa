import { Pipe, PipeTransform } from '@angular/core';

/**
 * Data della nota. L'utente sceglie solo il giorno (input di tipo date, nel proprio fuso orario); il
 * valore salvato è data e ora in UTC, con l'ora attuale del giorno scelto.
 */

const LONG_DATE = new Intl.DateTimeFormat('it-IT', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});
const SHORT_DATE = new Intl.DateTimeFormat('it-IT', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
});

/** Oggi nel formato dell'input date (yyyy-mm-dd), nel fuso orario dell'utente. */
export function todayInputValue(): string {
  return toInputValue(new Date());
}

/** Data ISO in UTC → valore dell'input date nel fuso orario dell'utente. */
export function inputValueOf(occurredAt: string): string {
  return toInputValue(new Date(occurredAt));
}

/**
 * Valore dell'input date → data e ora ISO in UTC. L'ora è quella di `timeOf` (di default adesso), così
 * le note dello stesso giorno restano nell'ordine in cui sono state create.
 */
export function toOccurredAt(inputValue: string, timeOf: Date = new Date()): string {
  const [year, month, day] = inputValue.split('-').map(Number);
  const date = new Date(timeOf);
  date.setFullYear(year, month - 1, day);
  return date.toISOString();
}

/** Titolo usato se l'utente non lo specifica: la data della nota, es. "1 novembre 2026". */
export function defaultTitle(inputValue: string): string {
  const [year, month, day] = inputValue.split('-').map(Number);
  return LONG_DATE.format(new Date(year, month - 1, day));
}

/** Nome del file senza estensione, per prepopolare il titolo degli upload. */
export function titleFromFileName(name: string): string {
  const dot = name.lastIndexOf('.');
  return (dot > 0 ? name.slice(0, dot) : name).slice(0, 200);
}

function toInputValue(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** Data della nota nella lista e nella pagina del record, es. "1 nov 2026". */
@Pipe({ name: 'noteDate' })
export class NoteDatePipe implements PipeTransform {
  transform(occurredAt: string, format: 'short' | 'long' = 'short'): string {
    return (format === 'long' ? LONG_DATE : SHORT_DATE).format(new Date(occurredAt));
  }
}
