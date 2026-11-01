/** Tempo massimo per leggere i metadati di un file. */
const READ_TIMEOUT_MS = 10_000;

/**
 * Margine sulla durata letta dal browser: per gli MP3 a bitrate variabile senza header Xing/VBRI la
 * durata è stimata. Nel margine decide la normalizzazione, che verifica la durata della copia.
 */
const DURATION_TOLERANCE = 1.05;

/**
 * Durata in secondi letta dal browser con un elemento audio, senza caricare il file. Null se il browser
 * non riconosce il formato (es. AMR/3GP in molti browser) o se la durata non è nell'header (WebM
 * registrati da un browser).
 */
export function readAudioDuration(file: Blob): Promise<number | null> {
  return new Promise((resolve) => {
    const audio = document.createElement('audio');
    const url = URL.createObjectURL(file);
    const timer = setTimeout(() => done(null), READ_TIMEOUT_MS);

    function done(seconds: number | null): void {
      clearTimeout(timer);
      audio.onloadedmetadata = null;
      audio.onerror = null;
      audio.removeAttribute('src');
      audio.load();
      URL.revokeObjectURL(url);
      resolve(seconds);
    }

    audio.preload = 'metadata';
    audio.onloadedmetadata = () =>
      done(Number.isFinite(audio.duration) && audio.duration > 0 ? audio.duration : null);
    audio.onerror = () => done(null);
    audio.src = url;
  });
}

/**
 * Vero se il browser riesce a leggere la durata del file e questa supera chiaramente il limite. Se la
 * durata non è leggibile il controllo resta alla normalizzazione.
 */
export async function exceedsMaxDuration(file: Blob, maxDurationMinutes: number): Promise<boolean> {
  const seconds = await readAudioDuration(file);
  return seconds !== null && seconds > maxDurationMinutes * 60 * DURATION_TOLERANCE;
}
