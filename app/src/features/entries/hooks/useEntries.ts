/* eslint-disable @typescript-eslint/no-unused-vars */
import { useState, useEffect } from 'react';
import type { Entry } from '../../../shared/types/types';
import { entriesService } from '../services/entriesService';

export const useEntries = () => {
  const [entries, setEntries] = useState<Entry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Carica le note all'inizio
  useEffect(() => {
    loadEntries();
  }, []);

  const loadEntries = async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await entriesService.getEntries();
      if (response.success && response.data) {
        setEntries(response.data);
      } else {
        setError(response.error || 'Errore nel caricamento delle note');
      }
    } catch (err) {
      setError('Errore imprevisto');
    } finally {
      setLoading(false);
    }
  };

  const deleteEntry = async (id: string) => {
    setError(null);
    try {
      const response = await entriesService.deleteEntry(id);
      if (response.success) {
        setEntries(prev => prev.filter(entry => entry.id !== id));
        return true;
      } else {
        setError(response.error || 'Errore nell\'eliminazione della nota');
        return false;
      }
    } catch (err) {
      setError('Errore imprevisto');
      return false;
    }
  };

  return {
    entries,
    loading,
    error,
    deleteEntry,
    loadEntries,
  };
};
