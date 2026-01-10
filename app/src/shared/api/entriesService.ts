/* eslint-disable @typescript-eslint/no-unused-vars */
import type { Entry, ApiResponse } from './types';

// Mock data - sarà sostituito con vere API call
const mockEntries: Entry[] = [
  {
    id: '1',
    title: 'Prima nota di prova',
    content: 'Questa è una nota di esempio per la home page',
    date: new Date().toLocaleDateString('it-IT'),
  },
  {
    id: '2',
    title: 'Seconda nota',
    content: 'Un\'altra nota per visualizzare la lista',
    date: new Date().toLocaleDateString('it-IT'),
  },
];

// Store in-memory delle note
let store: Entry[] = [...mockEntries];

// Configurazione API
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:3000/api';

export const entriesService = {
  /**
   * Recupera tutte le note
   */
  async getEntries(): Promise<ApiResponse<Entry[]>> {
    try {
      // Simuliamo un delay di rete
      await new Promise(resolve => setTimeout(resolve, 300));
      return {
        success: true,
        data: [...store],
      };
      // Future: return fetch(`${API_BASE_URL}/entries`).then(res => res.json());
    } catch (error) {
      return {
        success: false,
        error: 'Errore nel recupero delle note',
      };
    }
  },

  /**
   * Aggiunge una nuova nota
   */
  async addEntry(title: string, content: string): Promise<ApiResponse<Entry>> {
    try {
      // Simuliamo un delay di rete
      await new Promise(resolve => setTimeout(resolve, 200));
      
      const newEntry: Entry = {
        id: Date.now().toString(),
        title,
        content,
        date: new Date().toLocaleDateString('it-IT'),
      };

      store = [newEntry, ...store];
      return {
        success: true,
        data: newEntry,
      };
      // Future: return fetch(`${API_BASE_URL}/entries`, {
      //   method: 'POST',
      //   headers: { 'Content-Type': 'application/json' },
      //   body: JSON.stringify({ title, content }),
      // }).then(res => res.json());
    } catch (error) {
      return {
        success: false,
        error: 'Errore nell\'aggiunta della nota',
      };
    }
  },

  /**
   * Elimina una nota
   */
  async deleteEntry(id: string): Promise<ApiResponse<void>> {
    try {
      // Simuliamo un delay di rete
      await new Promise(resolve => setTimeout(resolve, 200));
      
      store = store.filter(entry => entry.id !== id);
      return {
        success: true,
      };
      // Future: return fetch(`${API_BASE_URL}/entries/${id}`, {
      //   method: 'DELETE',
      // }).then(res => res.json());
    } catch (error) {
      return {
        success: false,
        error: 'Errore nell\'eliminazione della nota',
      };
    }
  },
};
