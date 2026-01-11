import { useEffect } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { LoginPage } from '../features/auth/LoginPage';
import { HomePage } from '../features/entries/HomePage';
import { ErrorBoundary } from '../shared/components/ErrorBoundary';
import { setOnUnauthorizedCallback } from '../shared/lib/apiClient';
import { useAuth } from '../features/auth/AuthProvider';
import { Spinner } from '../shared/components/Spinner';
import { RootLayout } from './layouts/RootLayout';
import { ProtectedRoute } from '../features/auth/components/guards/ProtectedRoute';
import { PublicRoute } from '../features/auth/components/guards/PublicRoute';
import type { JSX } from 'react';

export function Router(): JSX.Element {
  const { isAuthenticated, isLoading, error, logout, resetAuthState } = useAuth();

  // Registra il callback per 401 Unauthorized nel API client
  // In questo modo il logout avviene tramite l'auth layer, mantenendo lo stato sincronizzato
  useEffect(() => {
    setOnUnauthorizedCallback(logout);
  }, [logout]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Spinner />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <p className="text-red-600 dark:text-red-400">{error}</p>
          <button
            type="button"
            className="mt-4 px-3 py-2 rounded bg-red-600 text-white hover:bg-red-700"
            onClick={() => logout()}
          >
            Riprova
          </button>
        </div>
      </div>
    );
  }

  const fallbackPath = isAuthenticated ? '/' : '/login';

  return (
    <ErrorBoundary onReset={resetAuthState}>
      <Routes>
        <Route element={<RootLayout isAuthenticated={isAuthenticated} />}>
          <Route
            path="/"
            element={<ProtectedRoute isAuthenticated={isAuthenticated}>{<HomePage />}</ProtectedRoute>}
          />
          <Route
            path="/login"
            element={<PublicRoute isAuthenticated={isAuthenticated}>{<LoginPage />}</PublicRoute>}
          />
          <Route path="*" element={<Navigate to={fallbackPath} replace />} />
        </Route>
      </Routes>
    </ErrorBoundary>
  );
}
