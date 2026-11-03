import { LongPressPoint } from './long-press';

/**
 * Posizione nel testo dell'elemento del carattere che si trova nel punto dello schermo, contando tutti i
 * nodi di testo (anche quelli delle evidenziazioni). Null se il punto non cade nel testo dell'elemento.
 */
export function textOffsetAt(element: HTMLElement, point: LongPressPoint): number | null {
  let node: Node | null = null;
  let offset = 0;
  if ('caretPositionFromPoint' in document) {
    const position = document.caretPositionFromPoint(point.x, point.y);
    node = position?.offsetNode ?? null;
    offset = position?.offset ?? 0;
  } else if ('caretRangeFromPoint' in document) {
    // Safari prima della versione 26 conosce solo questa.
    const range = (document as Document).caretRangeFromPoint(point.x, point.y);
    node = range?.startContainer ?? null;
    offset = range?.startOffset ?? 0;
  }
  if (!node || !element.contains(node)) {
    return null;
  }
  const range = document.createRange();
  range.setStart(element, 0);
  range.setEnd(node, offset);
  return range.toString().length;
}

/**
 * Porta il cursore del campo nella posizione indicata (o alla fine) senza far scorrere la pagina: il
 * campo prende il posto del testo con la stessa impaginazione, quindi il punto premuto resta dov'era.
 */
export function focusAt(field: HTMLTextAreaElement, offset: number | null): void {
  const at = offset ?? field.value.length;
  field.focus({ preventScroll: true });
  field.setSelectionRange(at, at);
}
