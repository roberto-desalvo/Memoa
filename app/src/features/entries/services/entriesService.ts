import type { Entry, CreateEntryRequest, ApiResponse } from '../../../shared/types/types';
import { apiClient } from '../../../shared/lib/apiClient';

export const entriesService = {
  /**
   * Recupera tutte le note dell'utente
   */
  async getEntries(): Promise<ApiResponse<Entry[]>> {
    const response = await apiClient.get<Entry[]>('/api/v1/entries');
    
    if (response.error) {
      return {
        success: false,
        error: response.error,
      };
    }

    return {
      success: true,
      data: response.data || [],
    };
  },

  /**
   * Aggiunge una nuova nota con supporto audio opzionale
   * @param entryId - ID dell'entry (già generato dal chiamante, usato per il path audio)
   * @param text - Contenuto testuale della nota
   * @param audioPath - Path dell'audio su Supabase (es: userId/entryId.webm)
   * @param audioMime - MIME type dell'audio
   * @param audioDurationMs - Durata registrazione in millisecondi
   * @param audioSizeBytes - Dimensione file in bytes
   */
  async addEntry(
    entryId: string,
    text: string,
    audioPath?: string,
    audioMime?: string,
    audioDurationMs?: number,
    audioSizeBytes?: number
  ): Promise<ApiResponse<Entry>> {
    const createRequest: CreateEntryRequest = {
      id: entryId,
      text,
      createdAt: new Date().toISOString(),
      audioPath,
      audioMime,
      audioDurationMs,
      audioSizeBytes,
    };

    const response = await apiClient.post<Entry>('/api/v1/entries', createRequest);
    
    if (response.error) {
      return {
        success: false,
        error: response.error,
      };
    }

    return {
      success: true,
      data: response.data,
    };
  },

  /**
   * Elimina una nota
   */
  async deleteEntry(id: string): Promise<ApiResponse<void>> {
    const response = await apiClient.delete(`/api/v1/entries/${id}`);
    
    if (response.error) {
      return {
        success: false,
        error: response.error,
      };
    }

    return {
      success: true,
    };
  },
};
