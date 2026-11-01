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
const DAY_MONTH = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'short' });
const MONTH = new Intl.DateTimeFormat('it-IT', { month: 'long', year: 'numeric' });
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

/** Titolo proposto per le registrazioni, nel fuso orario dell'utente: es. "REC_20261101_1435". */
export function recordingTitle(date: Date): string {
  return stampedTitle('REC', date);
}

/** Titolo dei testi scritti senza titolo, dalla data della nota: es. "TXT_20261101_1435". */
export function textTitle(date: Date): string {
  return stampedTitle('TXT', date);
}

function stampedTitle(prefix: string, date: Date): string {
  const [day, time] = toInputValue(date).split('T');
  return `${prefix}_${day.replaceAll('-', '')}_${time.replace(':', '')}`;
}

/** Nome del file senza estensione, usato come titolo degli upload. */
export function titleFromFileName(name: string): string {
  const dot = name.lastIndexOf('.');
  return (dot > 0 ? name.slice(0, dot) : name).trim().slice(0, 200);
}

/** Gruppo della lista: "Oggi", "Ieri", poi un gruppo per mese ("Gennaio 2026"). */
export interface ListGroup {
  key: string;
  label: string;
  /** Oggi o ieri: nella lista si mostra l'ora; negli altri gruppi giorno e mese ("27 gen"). */
  recent: boolean;
}

/** Gruppo della nota nella lista, nel fuso orario dell'utente. */
export function listGroup(date: Date, now: Date = new Date()): ListGroup {
  const day = dayKey(date);
  if (day === dayKey(now)) {
    return { key: day, label: 'Oggi', recent: true };
  }
  if (day === dayKey(new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1))) {
    return { key: day, label: 'Ieri', recent: true };
  }
  const month = MONTH.format(date);
  return { key: day.slice(0, 7), label: month[0].toUpperCase() + month.slice(1), recent: false };
}

/** Durata di un audio: "2:48", o "1:02:05" oltre l'ora. */
export function formatDuration(seconds: number): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${m}:${pad(s)}`;
}

/** Giorno nel fuso orario dell'utente (yyyy-mm-dd). */
function dayKey(date: Date): string {
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
 * Data della nota: breve ("1 nov 2026") nei risultati della ricerca; nella lista solo l'ora ("14:35")
 * per oggi e ieri o giorno e mese ("27 gen") nei gruppi per mese; estesa con l'ora nella pagina del
 * record.
 */
@Pipe({ name: 'noteDate' })
export class NoteDatePipe implements PipeTransform {
  transform(occurredAt: string, format: 'short' | 'dayMonth' | 'time' | 'long' = 'short'): string {
    const date = new Date(occurredAt);
    switch (format) {
      case 'long':
        return formatLong(date);
      case 'time':
        return TIME.format(date);
      case 'dayMonth':
        return DAY_MONTH.format(date);
      default:
        return SHORT_DATE.format(date);
    }
  }
}
