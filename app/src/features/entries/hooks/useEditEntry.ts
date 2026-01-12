import { useState, useEffect, useRef } from 'react';
import type { Entry } from '../../../shared/types/types';
import { useAudioRecorder, type AudioMetadata } from './useAudioRecorder';
import { entriesService } from '../services/entriesService';
import { supabaseStorage } from '../../../shared/lib/supabaseStorage';
import { useUser } from './useUser';

const MAX_TITLE_LENGTH = 200;
const MAX_TEXT_LENGTH = 100000;
const MAX_TRANSCRIPTION_LENGTH = 100000;

export const useEditEntry = (entry: Entry, onUpdate: (updatedEntry: Entry) => void) => {
  const { user } = useUser();
  const [isEditMode, setIsEditMode] = useState(false);
  const [editedTitle, setEditedTitle] = useState(entry.title);
  const [editedText, setEditedText] = useState(entry.text);
  const [editedTranscription, setEditedTranscription] = useState(entry.audioTranscription || '');
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Audio replacement
  const [newAudioBlob, setNewAudioBlob] = useState<Blob | null>(null);
  const [newAudioMetadata, setNewAudioMetadata] = useState<AudioMetadata | null>(null);
  const [newAudioPath, setNewAudioPath] = useState<string | null>(null);
  const [isUploadingAudio, setIsUploadingAudio] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const audioRecorder = useAudioRecorder();
  const newAudioPathRef = useRef<string | null>(null);

  // Track if there are unsaved changes
  const hasUnsavedChanges = 
    editedTitle !== entry.title ||
    editedText !== entry.text ||
    editedTranscription !== (entry.audioTranscription || '') ||
    newAudioPath !== null;

  // Validation
  const titleError = editedTitle.trim().length === 0 
    ? 'Il titolo non può essere vuoto'
    : editedTitle.length > MAX_TITLE_LENGTH
    ? `Il titolo non può superare i ${MAX_TITLE_LENGTH} caratteri`
    : null;

  const textError = editedText.trim().length === 0 
    ? 'Il testo non può essere vuoto'
    : editedText.length > MAX_TEXT_LENGTH
    ? `Il testo non può superare i ${MAX_TEXT_LENGTH.toLocaleString()} caratteri`
    : null;

  const transcriptionError = editedTranscription.length > MAX_TRANSCRIPTION_LENGTH
    ? `La trascrizione non può superare i ${MAX_TRANSCRIPTION_LENGTH.toLocaleString()} caratteri`
    : null;

  const isValid = !titleError && !textError && !transcriptionError;

  // Enter edit mode
  const enterEditMode = () => {
    setIsEditMode(true);
    setEditedTitle(entry.title);
    setEditedText(entry.text);
    setEditedTranscription(entry.audioTranscription || '');
    setError(null);
  };

  // Handle audio recording completion
  const handleAudioRecorded = async (blob: Blob, metadata: AudioMetadata) => {
    if (!user?.id) {
      setUploadError('Utente non autenticato');
      return;
    }

    setIsUploadingAudio(true);
    setUploadError(null);

    try {
      // Generate unique entryId with timestamp to avoid collisions
      const timestamp = Date.now();
      const uniqueEntryId = `${entry.id}_${timestamp}`;

      // Upload to Supabase - it will create path as: userId/uniqueEntryId.extension
      const { audioPath: uploadedPath, audioSizeBytes } = await supabaseStorage.uploadAudio(
        blob,
        user.id,
        uniqueEntryId,
        metadata.mimeType
      );

      setNewAudioBlob(blob);
      setNewAudioMetadata({ ...metadata, sizeBytes: audioSizeBytes });
      setNewAudioPath(uploadedPath);
      newAudioPathRef.current = uploadedPath;
      setUploadError(null);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore durante il caricamento dell\'audio';
      setUploadError(message);
    } finally {
      setIsUploadingAudio(false);
    }
  };

  // Discard new audio recording
  const discardNewAudio = async () => {
    if (newAudioPath) {
      try {
        await supabaseStorage.deleteAudio(newAudioPath);
      } catch (err) {
        console.error('Error deleting new audio:', err);
      }
    }
    setNewAudioBlob(null);
    setNewAudioMetadata(null);
    setNewAudioPath(null);
    newAudioPathRef.current = null;
    audioRecorder.resetRecording();
  };

  // Cancel edit mode with confirmation if there are unsaved changes
  const cancelEdit = async () => {
    if (hasUnsavedChanges) {
      const confirmed = window.confirm(
        'Hai modifiche non salvate. Sei sicuro di voler annullare?'
      );
      if (!confirmed) return;
    }

    // Cleanup orphan audio if uploaded but not saved
    if (newAudioPath) {
      try {
        await supabaseStorage.deleteAudio(newAudioPath);
      } catch (err) {
        console.error('Error cleaning up orphan audio:', err);
      }
    }

    setIsEditMode(false);
    setEditedTitle(entry.title);
    setEditedText(entry.text);
    setEditedTranscription(entry.audioTranscription || '');
    setNewAudioBlob(null);
    setNewAudioMetadata(null);
    setNewAudioPath(null);
    newAudioPathRef.current = null;
    setError(null);
    setUploadError(null);
    audioRecorder.resetRecording();
  };

  // Save changes
  const saveChanges = async () => {
    if (!isValid) return;

    setIsSaving(true);
    setError(null);

    try {
      console.log('Saving entry with audio:', {
        entryId: entry.id,
        hasNewAudio: !!newAudioPath,
        newAudioPath,
        audioMetadata: newAudioMetadata
      });

      const response = await entriesService.updateEntry(
        entry.id,
        editedTitle,
        editedText,
        editedTranscription || null,
        newAudioPath,
        newAudioMetadata?.mimeType || null,
        newAudioMetadata?.durationMs || null,
        newAudioMetadata?.sizeBytes || null
      );

      if (!response.success || !response.data) {
        throw new Error(response.error || 'Errore durante l\'aggiornamento');
      }

      console.log('Entry updated successfully:', response.data);

      // Success - update parent component
      onUpdate(response.data);
      
      // Reset edit state
      setIsEditMode(false);
      setNewAudioBlob(null);
      setNewAudioMetadata(null);
      setNewAudioPath(null);
      newAudioPathRef.current = null;
      audioRecorder.resetRecording();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore durante il salvataggio';
      setError(message);
    } finally {
      setIsSaving(false);
    }
  };

  // Cleanup orphan audio on unmount if not saved
  useEffect(() => {
    return () => {
      if (newAudioPathRef.current) {
        supabaseStorage.deleteAudio(newAudioPathRef.current).catch(err => {
          console.error('Error cleaning up orphan audio on unmount:', err);
        });
      }
    };
  }, []);

  return {
    // Edit mode state
    isEditMode,
    enterEditMode,
    cancelEdit,
    saveChanges,
    isSaving,
    error,

    // Text editing
    editedTitle,
    setEditedTitle,
    editedText,
    setEditedText,
    editedTranscription,
    setEditedTranscription,
    titleError,
    textError,
    transcriptionError,
    isValid,
    hasUnsavedChanges,

    // Audio recording
    audioRecorder,
    handleAudioRecorded,
    isUploadingAudio,
    uploadError,

    // Audio preview
    newAudioBlob,
    newAudioMetadata,
    newAudioPath,
    discardNewAudio,
  };
};
