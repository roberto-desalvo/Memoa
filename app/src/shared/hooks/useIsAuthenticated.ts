import { useAuth } from '../../features/auth/AuthProvider';

/**
 * Hook centralizzato per controllare se l'utente è veramente autenticato
 * Controlla SOLO se esiste una sessione valida da Supabase
 */
export const useIsAuthenticated = () => {
  const { isAuthenticated } = useAuth();
  
  return isAuthenticated;
};
