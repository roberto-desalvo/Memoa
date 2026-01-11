import { useState, useCallback } from 'react';
import { v4 as uuidv4 } from 'uuid';
import type { AudioMetadata } from './useAudioRecorder';
import { supabaseStorage } from '../../../shared/lib/supabaseStorage';
import { entriesService } from '../services/entriesService';
import { supabase } from '../../../shared/lib/supabaseClient';

export interface EntryFormState {
  content: string;
  validationError: string | null;
  audioBlob: Blob | null;
  audioMetadata: AudioMetadata | null;
  isSubmitting: boolean;
  submitError: string | null;
}

export interface UseEntryFormReturn extends EntryFormState {
  setContent: (content: string) => void;
  setValidationError: (error: string | null) => void;
  setAudioBlob: (blob: Blob | null, metadata: AudioMetadata | null) => void;
  clearAudio: () => void;
  validate: () => boolean;
  reset: () => void;
  submitEntry: () => Promise<boolean>;
}

export const useEntryForm = (onEntryAdded?: () => void): UseEntryFormReturn => {
  const [content, setContent] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [audioBlob, setAudioBlob] = useState<Blob | null>(null);
  const [audioMetadata, setAudioMetadata] = useState<AudioMetadata | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const validate = useCallback((): boolean => {
    if (!content.trim()) {
      setValidationError('Il contenuto della nota è obbligatorio');
      return false;
    }

    return true;
  }, [content]);

  const setAudioBlobWithMetadata = useCallback((blob: Blob | null, metadata: AudioMetadata | null) => {
    setAudioBlob(blob);
    setAudioMetadata(metadata);
  }, []);

  const clearAudio = useCallback(() => {
    setAudioBlob(null);
    setAudioMetadata(null);
  }, []);

  const reset = useCallback(() => {
    setContent('');
    setValidationError(null);
    setAudioBlob(null);
    setAudioMetadata(null);
    setSubmitError(null);
    setIsSubmitting(false); 
  }, []);

  const submitEntry = useCallback(async (): Promise<boolean> => {
    if (!validate()) {
      return false;
    }

    setIsSubmitting(true);
    setSubmitError(null);

    try {
      // Ottieni userId da sessione Supabase
      const { data: { session } } = await supabase.auth.getSession();
      const userId = session?.user?.id;

      if (!userId) {
        throw new Error('Utente non autenticato');
      }

      // Genera ID entry
      const entryId = uuidv4();
      let audioPath: string | undefined;
      let audioSizeBytes: number | undefined;

      // Se c'è audio, fai l'upload
      if (audioBlob && audioMetadata) {
        try {
          const uploadResult = await supabaseStorage.uploadAudio(
            audioBlob,
            userId,
            entryId,
            audioMetadata.mimeType
          );
          audioPath = uploadResult.audioPath;
          audioSizeBytes = uploadResult.audioSizeBytes;
        } catch (uploadError) {
          const message = uploadError instanceof Error ? uploadError.message : 'Errore upload audio';
          throw new Error(message);
        }
      }

      // POST /entries con tutti i campi
      const response = await entriesService.addEntry(
        content,
        audioPath,
        audioMetadata?.mimeType,
        audioMetadata?.durationMs,
        audioSizeBytes
      );

      if (!response.success) {
        throw new Error(response.error || 'Errore nell\'aggiunta della nota');
      }

      reset();
      onEntryAdded?.();
      return true;
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Errore nel salvataggio';
      setSubmitError(message);
      setIsSubmitting(false);
      return false;
    }
  }, [validate, reset, onEntryAdded]);

  return {
    content,
    setContent,
    validationError,
    setValidationError,
    audioBlob,
    audioMetadata,
    isSubmitting,
    submitError,
    setAudioBlob: setAudioBlobWithMetadata,
    clearAudio,
    validate,
    reset,
    submitEntry,
  };
};
