import React, { useState, useEffect, useCallback } from 'react';
import { supabase } from '../lib/supabaseClient';

export interface UseAuthSessionManagementReturn {
  isAuthenticated: boolean;
  accessToken: string | null;
  isLoading: boolean;
  logout: () => Promise<void>;
}

export const useAuthSessionManagement = (): UseAuthSessionManagementReturn => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isInitialized, setIsInitialized] = useState(false);

  // Traccia se un logout è stato appena eseguito
  const isExplicitLogoutRef = React.useRef<{ timestamp: number } | null>(null);

  // Ascolta i cambiamenti dello stato di autenticazione
  useEffect(() => {
    let isMounted = true;
    let isInitialCheck = true;

    const initializeAuth = async () => {
      try {
        const { data: { session } } = await supabase.auth.getSession();
        
        console.log('[Auth] Initial session check:', {
          hasSession: !!session,
          hasAccessToken: !!session?.access_token,
          token: session?.access_token ? session.access_token.substring(0, 20) + '...' : 'NONE',
        });

        if (isMounted) {
          const hasValidSession = !!session && !!session.access_token;
          setIsAuthenticated(hasValidSession);
          setAccessToken(hasValidSession ? session.access_token : null);
        }
      } catch (error) {
        console.error('[Auth] Error checking session:', error);
        if (isMounted) {
          setIsAuthenticated(false);
          setAccessToken(null);
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
          setIsInitialized(true);
        }
      }
    };

    initializeAuth();

    // Subscribe to auth state changes
    const { data: { subscription } } = supabase.auth.onAuthStateChange(
      (event, session) => {
        console.log('[Auth] Auth state change event:', event, {
          hasSession: !!session,
          hasAccessToken: !!session?.access_token,
          isExplicitLogout: !!isExplicitLogoutRef.current,
          token: session?.access_token ? session.access_token.substring(0, 20) + '...' : 'NONE',
        });

        // Skip the first INITIAL_SESSION event - it's already handled by initializeAuth
        if (isInitialCheck && (event === 'INITIAL_SESSION' || event === 'SIGNED_IN')) {
          console.log('[Auth] Skipping initial event');
          isInitialCheck = false;
          return;
        }

        // Skippa evento SIGNED_OUT subito dopo logout esplicito (entro 1 secondo)
        if (event === 'SIGNED_OUT' && isExplicitLogoutRef.current) {
          const timeSinceLogout = Date.now() - isExplicitLogoutRef.current.timestamp;
          if (timeSinceLogout < 1000) {
            console.log('[Auth] Skipping SIGNED_OUT event after explicit logout');
            return;
          }
        }

        if (isMounted) {
          const hasValidSession = !!session && !!session.access_token;
          setIsAuthenticated(hasValidSession);
          setAccessToken(hasValidSession ? session.access_token : null);
        }
      }
    );

    return () => {
      isMounted = false;
      subscription?.unsubscribe();
    };
  }, []);

  const logout = useCallback(async () => {
    try {
      console.log('[Auth] Logout initiated - setting loading flag');
      
      // Traccia il logout esplicito
      isExplicitLogoutRef.current = { timestamp: Date.now() };
      
      // Mantieni loading = true per coprire il flicker
      setIsLoading(true);
      
      await supabase.auth.signOut();
      
      if (isExplicitLogoutRef.current) {
        setIsAuthenticated(false);
        setAccessToken(null);
        
        // Mantieni loading per 600ms per coprire qualsiasi event dal listener
        await new Promise(resolve => setTimeout(resolve, 600));
        
        setIsLoading(false);
        
        // Pulisci il flag dopo 1 secondo
        setTimeout(() => {
          isExplicitLogoutRef.current = null;
        }, 1000);
      }
      
      console.log('[Auth] Logout complete');
    } catch (error) {
      console.error('[Auth] Error logging out:', error);
      setIsLoading(false);
      isExplicitLogoutRef.current = null;
    }
  }, []);

  return {
    isAuthenticated,
    accessToken,
    isLoading,
    logout,
  };
};
