/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useCallback, type ReactNode } from 'react';
import { useAuthSessionManagement } from '../../shared/hooks/useAuthSessionManagement';

interface AuthContextType {
  isAuthenticated: boolean;
  accessToken: string | null;
  isLoading: boolean;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider = ({ children }: AuthProviderProps) => {
  const { isAuthenticated, accessToken, isLoading, logout } =
    useAuthSessionManagement();

  const value: AuthContextType = {
    isAuthenticated,
    accessToken,
    isLoading,
    logout,
  };

  // Optional: mostra loading mentre verifica la sessione
  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-gray-600 dark:text-gray-400">Caricamento...</p>
      </div>
    );
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);

  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }

  return context;
};
