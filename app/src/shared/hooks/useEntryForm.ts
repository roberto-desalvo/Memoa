import { useState, useCallback } from 'react';

export interface EntryFormState {
  content: string;
  validationError: string | null;
}

export interface UseEntryFormReturn extends EntryFormState {
  setContent: (content: string) => void;
  setValidationError: (error: string | null) => void;
  validate: () => boolean;
  reset: () => void;
}

export const useEntryForm = (): UseEntryFormReturn => {
  const [content, setContent] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  const validate = useCallback((): boolean => {
    if (!content.trim()) {
      setValidationError('Il contenuto della nota è obbligatorio');
      return false;
    }

    return true;
  }, [content]);

  const reset = useCallback(() => {
    setContent('');
    setValidationError(null);
  }, []);

  return {
    content,
    setContent,
    validationError,
    setValidationError,
    validate,
    reset,
  };
};
