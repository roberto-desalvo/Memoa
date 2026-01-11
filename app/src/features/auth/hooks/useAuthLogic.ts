import { useState, useCallback } from 'react';
import { supabase } from '../../../shared/lib/supabaseClient';

export interface UseAuthLogicReturn {
  email: string;
  setEmail: (email: string) => void;
  loading: boolean;
  error: string;
  linkSent: boolean;
  sendMagicLink: (e: React.FormEvent<HTMLFormElement>) => Promise<void>;
  resetForm: () => void;
}

export const useAuthLogic = (): UseAuthLogicReturn => {
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [linkSent, setLinkSent] = useState(false);

  const sendMagicLink = useCallback(
    async (e: React.FormEvent<HTMLFormElement>) => {
      e.preventDefault();
      setError('');
      setLoading(true);

      try {
        const { error: signInError } = await supabase.auth.signInWithOtp({
          email,
          options: {
            shouldCreateUser: true,
          },
        });

        if (signInError) {
          setError(signInError.message);
        } else {
          setLinkSent(true);
        }
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : "Errore durante l'invio del link"
        );
      } finally {
        setLoading(false);
      }
    },
    []
  );

  const resetForm = useCallback(() => {
    setEmail('');
    setError('');
    setLinkSent(false);
  }, []);

  return {
    email,
    setEmail,
    loading,
    error,
    linkSent,
    sendMagicLink,
    resetForm,
  };
};
