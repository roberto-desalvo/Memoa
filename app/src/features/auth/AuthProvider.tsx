/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useMemo, type JSX, type ReactNode } from 'react';
import { useAuthSessionManagement } from './hooks/useAuthSessionManagement';

interface AuthContextType {
  isAuthenticated: boolean;
  accessToken: string | null;
  isLoading: boolean;
  error: string | null;
  logout: () => void;
  refreshSession: () => Promise<void>;
  clearError: () => void;
  resetAuthState: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider = ({ children }: AuthProviderProps): JSX.Element => {
  const { isAuthenticated, accessToken, isLoading, error, logout, refreshSession, clearError, resetAuthState } =
    useAuthSessionManagement();

  const value: AuthContextType = useMemo(
    () => ({ isAuthenticated, accessToken, isLoading, error, logout, refreshSession, clearError, resetAuthState }),
    [isAuthenticated, accessToken, isLoading, error, logout, refreshSession, clearError, resetAuthState]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);

  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }

  return context;
};
