import { supabase } from './supabaseClient';
import type { ApiSuccessResult, ApiResponse, ProblemDetails } from '../types/types';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5055';

// Callback type for unauthorized handling
type OnUnauthorizedCallback = () => Promise<void> | void;
let onUnauthorizedCallback: OnUnauthorizedCallback | null = null;

/**
 * Registra un callback da eseguire quando viene rilevato un 401 Unauthorized.
 * Questo permette al router/auth layer di gestire il logout in modo coerente.
 */
export const setOnUnauthorizedCallback = (callback: OnUnauthorizedCallback): void => {
  onUnauthorizedCallback = callback;
};

class ApiClient {
  private async getAuthToken(): Promise<string | null> {
    try {
      const { data: { session } } = await supabase.auth.getSession();
      return session?.access_token || null;
    } catch (error) {
      console.error('Error getting auth token:', error);
      return null;
    }
  }

  private async handleUnauthorized(): Promise<void> {
    try {
      // Path 1: Se un callback è registrato (preferito), delegare al router/auth layer
      if (onUnauthorizedCallback) {
        await onUnauthorizedCallback();
        return;
      }
      
      // Path 2: Fallback - logout diretto da Supabase (se nessun callback è registrato)
      await supabase.auth.signOut();
    } catch (error) {
      console.error('Error during logout:', error);
    }
  }

  private buildHeaders(token: string | null): HeadersInit {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    return headers;
  }

  private async parseErrorResponse(response: Response): Promise<string> {
    try {
      const problemDetails: ProblemDetails = await response.json();
      return problemDetails.detail || problemDetails.title || `HTTP ${response.status}`;
    } catch {
      return `HTTP ${response.status}: ${response.statusText}`;
    }
  }

  async request<T = unknown>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<ApiResponse<T>> {
    try {
      const token = await this.getAuthToken();
      const url = `${API_URL}${endpoint}`;
      const headers = this.buildHeaders(token);

      const response = await fetch(url, {
        ...options,
        headers,
      });

      // Handle 401 Unauthorized
      if (response.status === 401) {
        await this.handleUnauthorized();
        return { success: false, error: 'Unauthorized', status: 401 };
      }

      // Handle 204 No Content
      if (response.status === 204) {
        return { success: true, data: undefined as T, status: 204 };
      }

      // For error responses, try to parse as ProblemDetails
      if (!response.ok) {
        const error = await this.parseErrorResponse(response);
        return { success: false, error, status: response.status };
      }

      // Response is wrapped in ApiSuccessResult<T> for successful responses
      const apiResult: ApiSuccessResult<T> = await response.json();

      if (apiResult.data !== undefined) {
        return { success: true, data: apiResult.data, status: response.status };
      }

      return {
        success: false,
        error: apiResult.resultDescription || 'No data returned',
        status: response.status,
      };
    } catch (error) {
      console.error('API request error:', error);
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error',
        status: 0,
      };
    }
  }

  get<T = unknown>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>> {
    return this.request<T>(endpoint, { ...options, method: 'GET' });
  }

  post<T = unknown>(endpoint: string, body?: unknown, options?: RequestInit): Promise<ApiResponse<T>> {
    return this.request<T>(endpoint, {
      ...options,
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  put<T = unknown>(endpoint: string, body?: unknown, options?: RequestInit): Promise<ApiResponse<T>> {
    return this.request<T>(endpoint, {
      ...options,
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  delete<T = unknown>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>> {
    return this.request<T>(endpoint, { ...options, method: 'DELETE' });
  }
}

export const apiClient = new ApiClient();
