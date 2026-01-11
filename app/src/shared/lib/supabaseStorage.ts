import { supabase } from './supabaseClient';

const BUCKET_NAME = 'audio';

export const supabaseStorage = {
  /**
   * Upload blob audio a Supabase Storage
   * Path: audio/{userId}/{entryId}.webm
   */
  async uploadAudio(
    blob: Blob,
    userId: string,
    entryId: string,
    mimeType: string
  ): Promise<{ audioPath: string; audioSizeBytes: number }> {
    // Determina l'estensione dal mime type
    const getExtension = (mime: string): string => {
      if (mime.includes('webm')) return 'webm';
      if (mime.includes('mp4')) return 'mp4';
      if (mime.includes('wav')) return 'wav';
      return 'webm'; // default
    };

    const extension = getExtension(mimeType);
    const audioPath = `${userId}/${entryId}.${extension}`;

    // Converte Blob a File
    const file = new File([blob], `${entryId}.${extension}`, { type: mimeType });

    try {
      const { error } = await supabase.storage
        .from(BUCKET_NAME)
        .upload(audioPath, file);

      if (error) {
        throw new Error(`Upload fallito: ${error.message}`);
      }

      return {
        audioPath,
        audioSizeBytes: blob.size,
      };
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore upload audio';
      throw new Error(`Errore caricamento audio: ${message}`);
    }
  },

  /**
   * Genera Signed URL per accedere al file audio
   * URL valida per 24 ore (86400 secondi)
   */
  async getSignedUrl(audioPath: string, expiresIn: number = 86400): Promise<string> {
    try {
      const { data, error } = await supabase.storage
        .from(BUCKET_NAME)
        .createSignedUrl(audioPath, expiresIn);

      if (error) {
        throw new Error(`Errore generazione URL: ${error.message}`);
      }

      if (!data || !data.signedUrl) {
        throw new Error('URL firmato non generato');
      }

      return data.signedUrl;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore accesso audio';
      throw new Error(`Errore accesso audio: ${message}`);
    }
  },

  /**
   * Cancella file audio da Supabase Storage
   */
  async deleteAudio(audioPath: string): Promise<void> {
    try {
      const { error } = await supabase.storage
        .from(BUCKET_NAME)
        .remove([audioPath]);

      if (error) {
        throw new Error(`Errore cancellazione: ${error.message}`);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore cancellazione audio';
      throw new Error(`Errore cancellazione audio: ${message}`);
    }
  },
};
