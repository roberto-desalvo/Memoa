/* eslint-disable @typescript-eslint/no-unused-vars */
import type { Entry, CreateEntryRequest, ApiResponse } from './types';
import { supabase } from '../lib/supabaseClient';

// Configurazione API
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

// Funzione helper per ottenere il token JWT da Supabase
const getAuthToken = async (): Promise<string | null> => {
  try {
    console.log('[API] Getting auth token...');
    const { data: { session } } = await supabase.auth.getSession();
    const token = session?.access_token || null;
    
    console.log('[API] Token retrieval result:', {
      hasSession: !!session,
      hasToken: !!token,
      token: token ? token.substring(0, 20) + '...' : 'NULL',
    });
    
    if (!token) {
      console.warn('[API] ⚠️ No auth token available');
    } else {
      console.log('[API] ✓ Using token:', token.substring(0, 20) + '...');
    }
    
    return token;
  } catch (error) {
    console.error('[API] ❌ Error getting auth token:', error);
    return null;
  }
};

// Funzione helper per le richieste autenticate
const fetchWithAuth = async (url: string, options: RequestInit = {}) => {
  console.log('[API] fetchWithAuth called for:', url);
  const token = await getAuthToken();
  
  if (!token) {
    console.error('[API] ❌ No token available, throwing error');
    throw new Error('No authentication token found');
  }

  console.log('[API] ✓ Adding auth header with token');
  const headers = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`,
    ...options.headers,
  };

  return fetch(url, { ...options, headers });
};

export const entriesService = {
  /**
   * Recupera tutte le note dell'utente
   */
  async getEntries(): Promise<ApiResponse<Entry[]>> {
    try {
      console.log('[API] getEntries() called, fetching from:', `${API_BASE_URL}/api/v1/entries`);
      const response = await fetchWithAuth(`${API_BASE_URL}/api/v1/entries`);
      
      console.log('[API] Response status:', response.status);
      
      if (!response.ok) {
        if (response.status === 401) {
          console.error('[API] ❌ Unauthorized (401)');
          throw new Error('Non autorizzato');
        }
        console.error('[API] ❌ Non-OK response:', response.status);
        throw new Error('Errore nel recupero delle note');
      }

      const result = await response.json();
      console.log('[API] ✓ Entries fetched successfully:', result);
      return {
        success: true,
        data: result.data || [],
      };
    } catch (error) {
      console.error('[API] ❌ getEntries error:', error);
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Errore nel recupero delle note',
      };
    }
  },

  /**
   * Aggiunge una nuova nota con supporto audio opzionale
   */
  async addEntry(
    text: string,
    audioPath?: string,
    audioMime?: string,
    audioDurationMs?: number,
    audioSizeBytes?: number
  ): Promise<ApiResponse<Entry>> {
    try {
      const createRequest: CreateEntryRequest = {
        id: crypto.randomUUID(),
        text,
        createdAt: new Date().toISOString(),
        audioPath,
        audioMime,
        audioDurationMs,
        audioSizeBytes,
      };

      const response = await fetchWithAuth(`${API_BASE_URL}/api/v1/entries`, {
        method: 'POST',
        body: JSON.stringify(createRequest),
      });

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Non autorizzato');
        }
        throw new Error('Errore nell\'aggiunta della nota');
      }

      const result = await response.json();
      return {
        success: true,
        data: result.data,
      };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Errore nell\'aggiunta della nota',
      };
    }
  },

  /**
   * Elimina una nota
   */
  async deleteEntry(id: string): Promise<ApiResponse<void>> {
    try {
      const response = await fetchWithAuth(`${API_BASE_URL}/api/v1/entries/${id}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Non autorizzato');
        }
        throw new Error('Errore nell\'eliminazione della nota');
      }

      return {
        success: true,
      };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Errore nell\'eliminazione della nota',
      };
    }
  },
};
