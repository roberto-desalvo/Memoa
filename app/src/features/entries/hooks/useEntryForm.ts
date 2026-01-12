import { useState, useCallback } from 'react';
import { v4 as uuidv4 } from 'uuid';
import type { AudioMetadata } from './useAudioRecorder';
import { supabaseStorage } from '../../../shared/lib/supabaseStorage';
import { entriesService } from '../services/entriesService';
import { supabase } from '../../../shared/lib/supabaseClient';

export interface EntryFormState {
  title: string;
  content: string;
  validationError: string | null;
  audioBlob: Blob | null;
  audioMetadata: AudioMetadata | null;
  isSubmitting: boolean;
  submitError: string | null;
  isUploadingAudio: boolean;
  uploadProgress: number; // 0-100
}

export interface UseEntryFormReturn extends EntryFormState {
  setTitle: (title: string) => void;
  setContent: (content: string) => void;
  setValidationError: (error: string | null) => void;
  setAudioBlob: (blob: Blob | null, metadata: AudioMetadata | null) => void;
  clearAudio: () => void;
  validate: () => boolean;
  reset: () => void;
  submitEntry: () => Promise<boolean>;
}

/**
 * Hook per la gestione del form di creazione entries con audio
 * Gestisce:
 * - Validazione contenuto
 * - Upload audio con path pattern: audio/{userId}/{entryId}.webm
 * - Salvataggio metadati (mime, durata, size)
 * - Gestione errori con feedback specifici (es: permission denied)
 */
export const useEntryForm = (onEntryAdded?: () => void): UseEntryFormReturn => {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [audioBlob, setAudioBlob] = useState<Blob | null>(null);
  const [audioMetadata, setAudioMetadata] = useState<AudioMetadata | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isUploadingAudio, setIsUploadingAudio] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);

  const validate = useCallback((): boolean => {
    if (!title.trim()) {
      setValidationError('Il titolo della nota è obbligatorio');
      return false;
    }

    if (!content.trim()) {
      setValidationError('Il contenuto della nota è obbligatorio');
      return false;
    }

    return true;
  }, [title, content]);

  const setAudioBlobWithMetadata = useCallback((blob: Blob | null, metadata: AudioMetadata | null) => {
    setAudioBlob(blob);
    setAudioMetadata(metadata);
  }, []);

  const clearAudio = useCallback(() => {
    setAudioBlob(null);
    setAudioMetadata(null);
  }, []);

  const reset = useCallback(() => {
    setTitle('');
    setContent('');
    setValidationError(null);
    setAudioBlob(null);
    setAudioMetadata(null);
    setSubmitError(null);
    setIsSubmitting(false);
    setIsUploadingAudio(false);
    setUploadProgress(0);
  }, []);

  /**
   * Interpreta errore upload e fornisce messaggio user-friendly
   */
  const getUploadErrorMessage = (error: unknown): string => {
    const message = error instanceof Error ? error.message : 'Errore sconosciuto';
    
    if (message.includes('Access Denied') || message.includes('Unauthorized')) {
      return 'Accesso negato. Assicurati di essere loggato e che il percorso sia corretto (audio/{userId}/{entryId}.webm)';
    }
    if (message.includes('No such bucket')) {
      return 'Bucket di archiviazione non trovato. Contatta il supporto.';
    }
    if (message.includes('404')) {
      return 'File non trovato durante l\'upload. Riprova.';
    }
    if (message.includes('413') || message.includes('Payload too large')) {
      return 'File troppo grande. Massimo 50MB.';
    }
    if (message.includes('Network') || message.includes('timeout')) {
      return 'Errore di connessione. Verifica la tua rete e riprova.';
    }
    
    return `Errore upload: ${message}`;
  };

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
        throw new Error('Utente non autenticato. Accedi per continuare.');
      }

      // Genera ID entry
      const entryId = uuidv4();
      let audioPath: string | undefined;
      let audioSizeBytes: number | undefined;

      // Se c'è audio, fai l'upload
      if (audioBlob && audioMetadata) {
        setIsUploadingAudio(true);
        setUploadProgress(0);
        
        try {
          // Simulare progress (l'SDK Supabase non fornisce hook di progress)
          const progressInterval = setInterval(() => {
            setUploadProgress((prev) => Math.min(prev + 10, 90));
          }, 200);

          const uploadResult = await supabaseStorage.uploadAudio(
            audioBlob,
            userId,
            entryId,
            audioMetadata.mimeType
          );
          
          clearInterval(progressInterval);
          setUploadProgress(100);
          
          audioPath = uploadResult.audioPath;
          audioSizeBytes = uploadResult.audioSizeBytes;
          
          setIsUploadingAudio(false);
        } catch (uploadError) {
          setIsUploadingAudio(false);
          setUploadProgress(0);
          const friendlyMessage = getUploadErrorMessage(uploadError);
          throw new Error(friendlyMessage);
        }
      }

      // POST /entries con tutti i campi
      // Nota: entryId è lo stesso usato per uploadare l'audio su Supabase
      const response = await entriesService.addEntry(
        entryId,
        title,
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
    title,
    setTitle,
    content,
    setContent,
    validationError,
    setValidationError,
    audioBlob,
    audioMetadata,
    isSubmitting,
    submitError,
    isUploadingAudio,
    uploadProgress,
    setAudioBlob: setAudioBlobWithMetadata,
    clearAudio,
    validate,
    reset,
    submitEntry,
  };
};
