/** Dimensione massima di un file audio caricato, prima della normalizzazione (come nelle Functions). */
export const MAX_AUDIO_SIZE_BYTES = 350 * 1024 * 1024;

/**
 * Formati audio accettati, per estensione: MP3, M4A/AAC, WAV, FLAC, Ogg/Opus, WebM, AMR/3GP.
 * Il controllo nel browser è solo un primo filtro: il formato reale viene verificato con ffprobe
 * durante la normalizzazione (che scarta anche i file video).
 */
const AUDIO_TYPES_BY_EXTENSION: Record<string, string> = {
  mp3: 'audio/mpeg',
  m4a: 'audio/mp4',
  aac: 'audio/aac',
  wav: 'audio/wav',
  flac: 'audio/flac',
  ogg: 'audio/ogg',
  oga: 'audio/ogg',
  opus: 'audio/opus',
  webm: 'audio/webm',
  amr: 'audio/amr',
  '3gp': 'audio/3gpp',
};

/** Valore per l'attributo `accept` dell'input file. */
export const AUDIO_ACCEPT = Object.keys(AUDIO_TYPES_BY_EXTENSION)
  .map((extension) => `.${extension}`)
  .join(',');

/** Etichetta leggibile dei formati, per i messaggi all'utente. */
export const AUDIO_FORMATS_LABEL = 'MP3, M4A, AAC, WAV, FLAC, OGG, Opus, WebM, AMR, 3GP';

/**
 * Tipo MIME da salvare sul blob, o `undefined` se l'estensione non è ammessa. Si basa
 * sull'estensione perché il tipo indicato dal browser manca o varia per AMR, 3GP e Opus.
 */
export function audioContentType(file: File): string | undefined {
  const extension = file.name.split('.').pop()?.toLowerCase() ?? '';
  return Object.hasOwn(AUDIO_TYPES_BY_EXTENSION, extension)
    ? AUDIO_TYPES_BY_EXTENSION[extension]
    : undefined;
}
