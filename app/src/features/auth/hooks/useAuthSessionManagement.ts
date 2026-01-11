import React, { useState, useEffect, useCallback } from 'react';
import { supabase } from '../../../shared/lib/supabaseClient';
import type { Session } from '@supabase/supabase-js';

// Magic number constants for clarity and maintainability
const LOGOUT_DELAY_MS = 600; // Delay after signOut to cover async listener events
const LOGOUT_FLAG_CLEANUP_MS = 1000; // Time window to recognize SIGNED_OUT after explicit logout
const LOGOUT_FLAG_TIMEOUT_MS = 1000; // Timeout to reset the explicit logout flag

// Utility function for delays (more testable than inline Promise)
const delay = (ms: number): Promise<void> => 
  new Promise(resolve => setTimeout(resolve, ms));

export interface UseAuthSessionManagementReturn {
  isAuthenticated: boolean;
  accessToken: string | null;
  isLoading: boolean;
  error: string | null;
  logout: () => Promise<void>;
  refreshSession: () => Promise<void>;
  clearError: () => void;
  resetAuthState: () => void;
}

// Helper function to check if a session is valid (avoids duplication)
const _isSessionValid = (session: Session | null): boolean => {
  return !!session && !!session.access_token;
};

// Helper function to generate context-aware error messages
const _getErrorMessage = (operation: 'init' | 'logout' | 'refresh', error: unknown): string => {
  const message = error instanceof Error ? error.message : 'Unknown error';
  const operationLabel = {
    init: 'Errore durante verifica sessione',
    logout: 'Errore durante logout',
    refresh: 'Errore durante aggiornamento sessione',
  }[operation];
  return `${operationLabel}: ${message}`;
};

// Helper function for development logging
const _log = (label: string, data?: unknown): void => {
  if (import.meta.env.DEV) {
    console.log(`[Auth] ${label}`, data || '');
  }
};

const _logError = (label: string, error?: unknown): void => {
  if (import.meta.env.DEV) {
    console.error(`[Auth] ${label}`, error || '');
  }
};

export const useAuthSessionManagement = (): UseAuthSessionManagementReturn => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isOperationInProgress, setIsOperationInProgress] = useState(false);

  // Track if logout was just executed (to ignore immediate SIGNED_OUT event from listener)
  const isExplicitLogoutRef = React.useRef<{ timestamp: number } | null>(null);

  // Ascolta i cambiamenti dello stato di autenticazione
  useEffect(() => {
    let isMounted = true;
    let isInitialCheck = true;

    const initializeAuth = async () => {
      try {
        const response = await supabase.auth.getSession();
        // Type guard: ensure response has expected shape
        if (!response || !response.data) {
          throw new Error('Invalid session response from Supabase');
        }
        const { session } = response.data;
        
        if (isMounted) {
          const hasValidSession = _isSessionValid(session);
          setIsAuthenticated(hasValidSession);
          setAccessToken(hasValidSession ? session?.access_token || null : null);
          setError(null);
          _log('Initial session check completed', { hasSession: hasValidSession });
        }
      } catch (error) {
        _logError('Error checking session', error);
        if (isMounted) {
          setIsAuthenticated(false);
          setAccessToken(null);
          setError(_getErrorMessage('init', error));
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    };

    initializeAuth();

    // Subscribe to auth state changes
    const { data: { subscription } } = supabase.auth.onAuthStateChange(
      (event, session) => {
        // Skip the first INITIAL_SESSION event - it's already handled by initializeAuth
        if (isInitialCheck && (event === 'INITIAL_SESSION' || event === 'SIGNED_IN')) {
          isInitialCheck = false;
          return;
        }

        // Skip SIGNED_OUT immediately after explicit logout (within configured window)
        if (event === 'SIGNED_OUT' && isExplicitLogoutRef.current) {
          const timeSinceLogout = Date.now() - isExplicitLogoutRef.current.timestamp;
          if (timeSinceLogout < LOGOUT_FLAG_TIMEOUT_MS) {
            _log('Skipping SIGNED_OUT event after explicit logout');
            return;
          }
        }

        if (isMounted) {
          const hasValidSession = _isSessionValid(session);
          setIsAuthenticated(hasValidSession);
          setAccessToken(hasValidSession ? session?.access_token || null : null);
          setError(null);
        }
      }
    );

    return () => {
      isMounted = false;
      subscription?.unsubscribe();
      // Reset explicit logout flag on unmount to prevent ref leaks
      isExplicitLogoutRef.current = null;
    };
  }, []);

  const logout = useCallback(async () => {
    // Prevent concurrent operations (race condition)
    if (isOperationInProgress) {
      _log('Logout already in progress, ignoring duplicate request');
      return;
    }

    try {
      setIsOperationInProgress(true);
      _log('Logout initiated');
      
      // Track explicit logout to ignore the immediate SIGNED_OUT event from listener
      isExplicitLogoutRef.current = { timestamp: Date.now() };
      
      // Maintain loading state to prevent UI flicker
      setIsLoading(true);
      setError(null);
      
      await supabase.auth.signOut();
      
      if (isExplicitLogoutRef.current) {
        setIsAuthenticated(false);
        setAccessToken(null);
        
        // Delay to cover async listener events from Supabase
        await delay(LOGOUT_DELAY_MS);
        
        setIsLoading(false);
        
        // Reset explicit logout flag after timeout window
        setTimeout(() => {
          isExplicitLogoutRef.current = null;
        }, LOGOUT_FLAG_CLEANUP_MS);
      }
      
      _log('Logout complete');
    } catch (error) {
      _logError('Error logging out', error);
      setIsLoading(false);
      isExplicitLogoutRef.current = null;
      setError(_getErrorMessage('logout', error));
    } finally {
      setIsOperationInProgress(false);
    }
  }, [isOperationInProgress]);

  const refreshSession = useCallback(async () => {
    // Prevent concurrent operations (race condition)
    if (isOperationInProgress) {
      _log('Session refresh already in progress, ignoring duplicate request');
      return;
    }

    try {
      setIsOperationInProgress(true);
      setIsLoading(true);
      setError(null);
      
      const response = await supabase.auth.getSession();
      // Type guard: ensure response has expected shape
      if (!response || !response.data) {
        throw new Error('Invalid session response from Supabase');
      }
      const { session } = response.data;

      const hasValidSession = _isSessionValid(session);
      setIsAuthenticated(hasValidSession);
      setAccessToken(hasValidSession ? session?.access_token || null : null);
      _log('Session refresh completed', { hasSession: hasValidSession });
    } catch (error) {
      _logError('Error refreshing session', error);
      setError(_getErrorMessage('refresh', error));
      setIsAuthenticated(false);
      setAccessToken(null);
    } finally {
      setIsLoading(false);
      setIsOperationInProgress(false);
    }
  }, [isOperationInProgress]);

  const clearError = useCallback(() => {
    setError(null);
  }, []);

  const resetAuthState = useCallback(() => {
    setIsAuthenticated(false);
    setAccessToken(null);
    setError(null);
    setIsLoading(false);
    setIsOperationInProgress(false);
  }, []);

  return {
    isAuthenticated,
    accessToken,
    isLoading,
    error,
    logout,
    refreshSession,
    clearError,
    resetAuthState,
  };
};
