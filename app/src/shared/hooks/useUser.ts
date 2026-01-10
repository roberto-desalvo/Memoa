import { useState, useEffect } from 'react';
import { useAuth } from '../../features/auth/AuthProvider';
import { apiClient } from '../lib/apiClient';
import type { UserDto } from '../api/types';

export const useUser = () => {
  const { accessToken } = useAuth();
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    const fetchUser = async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await apiClient.get<UserDto>('/api/v1/user');
        if (response.data) {
          setUser(response.data);
        } else {
          setError(response.error || 'Errore nel caricamento dati utente');
        }
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : 'Errore nel caricamento dati utente'
        );
      } finally {
        setLoading(false);
      }
    };

    fetchUser();
  }, [accessToken]);

  return { user, loading, error };
};
