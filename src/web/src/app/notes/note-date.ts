import { Pipe, PipeTransform } from '@angular/core';

/**
 * Data e ora della nota. L'utente le sceglie nel proprio fuso orario (input di tipo datetime-local, al
 * minuto); il valore salvato è in UTC.
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
const TIME = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' });

/** Adesso nel formato dell'input datetime-local (yyyy-mm-ddThh:mm), nel fuso orario dell'utente. */
export function nowInputValue(): string {
  return toInputValue(new Date());
}

/** Data ISO in UTC → valore dell'input datetime-local nel fuso orario dell'utente. */
export function inputValueOf(occurredAt: string): string {
  return toInputValue(new Date(occurredAt));
}

/**
 * Valore dell'input datetime-local → data e ora ISO in UTC. Secondi e millisecondi sono quelli di
 * `timeOf` (di default adesso), così le note dello stesso minuto restano nell'ordine in cui sono state
 * create.
 */
export function toOccurredAt(inputValue: string, timeOf: Date = new Date()): string {
  // Data e ora senza fuso orario: interpretate nel fuso orario dell'utente.
  const date = new Date(inputValue);
  date.setSeconds(timeOf.getSeconds(), timeOf.getMilliseconds());
  return date.toISOString();
}

/**
 * Titolo usato se l'utente non lo specifica e per le registrazioni: data e ora della nota, es.
 * "1 novembre 2026, 14:35".
 */
export function defaultTitle(date: Date): string {
  return formatLong(date);
}

/** Nome del file senza estensione, usato come titolo degli upload. */
export function titleFromFileName(name: string): string {
  const dot = name.lastIndexOf('.');
  return (dot > 0 ? name.slice(0, dot) : name).trim().slice(0, 200);
}

/**
 * Giorno della nota, per raggruppare la lista: "Oggi", "Ieri" o la data estesa ("2 gennaio 2026"),
 * nel fuso orario dell'utente.
 */
export function dayLabel(date: Date, now: Date = new Date()): string {
  const day = dayKey(date);
  if (day === dayKey(now)) {
    return 'Oggi';
  }
  if (day === dayKey(new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1))) {
    return 'Ieri';
  }
  return LONG_DATE.format(date);
}

/** Giorno nel fuso orario dell'utente (yyyy-mm-dd). */
export function dayKey(date: Date): string {
  return toInputValue(date).slice(0, 10);
}

function toInputValue(date: Date): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

function formatLong(date: Date): string {
  return `${LONG_DATE.format(date)}, ${TIME.format(date)}`;
}

/**
 * Data della nota: breve ("1 nov 2026") nei risultati della ricerca, solo l'ora ("14:35") nella lista
 * raggruppata per giorno, estesa con l'ora nella pagina del record.
 */
@Pipe({ name: 'noteDate' })
export class NoteDatePipe implements PipeTransform {
  transform(occurredAt: string, format: 'short' | 'time' | 'long' = 'short'): string {
    const date = new Date(occurredAt);
    switch (format) {
      case 'long':
        return formatLong(date);
      case 'time':
        return TIME.format(date);
      default:
        return SHORT_DATE.format(date);
    }
  }
}
