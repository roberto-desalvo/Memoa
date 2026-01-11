import { useState, useEffect } from 'react';
import { useAuth } from '../../auth/AuthProvider';
import { apiClient } from '../../../shared/lib/apiClient';
import type { UserDto } from '../../../shared/types/types';

export const useUser = () => {
  const { accessToken } = useAuth();
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    const abortController = new AbortController();

    const fetchUser = async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await apiClient.get<UserDto>('/api/v1/user');
        
        // Ignora la response se il component è unmounted o se il token è cambiato
        if (abortController.signal.aborted) {
          return;
        }

        if (response.data) {
          setUser(response.data);
        } else {
          setError(response.error || 'Errore nel caricamento dati utente');
        }
      } catch (err) {
        if (abortController.signal.aborted) {
          return;
        }
        setError(
          err instanceof Error
            ? err.message
            : 'Errore nel caricamento dati utente'
        );
      } finally {
        if (!abortController.signal.aborted) {
          setLoading(false);
        }
      }
    };

    fetchUser();

    return () => {
      abortController.abort();
    };
  }, [accessToken]);

  return { user, loading, error };
};
