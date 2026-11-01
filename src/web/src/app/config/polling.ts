/**
 * Intervalli del polling, uguali per note e tag. Il polling è attivo solo mentre la pagina è aperta, l'app
 * è visibile e qualcosa è ancora in corso (una nota in elaborazione, un tag in assegnazione); il controllo
 * successivo parte solo dopo la risposta, quindi le richieste non si sovrappongono.
 */

/** Liste (homepage, elenco dei tag): intervallo lungo, la risposta contiene tutti gli elementi. */
export const LIST_POLL_INTERVAL_MS = 10_000;

/** Pagina di un singolo elemento (nota, tag): intervallo breve, perché l'avanzamento sembri in tempo reale. */
export const PAGE_POLL_INTERVAL_MS = 1_000;
