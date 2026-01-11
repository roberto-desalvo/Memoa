import { Routes, Route, Outlet, Navigate } from 'react-router-dom';
import { LoginPage } from '../features/auth/LoginPage';
import { HomePage } from '../features/entries/HomePage';
import { Header } from '../shared/Header';
import { useAuth } from '../features/auth/AuthProvider';

const RootLayout = () => {
  const { isAuthenticated } = useAuth();

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {isAuthenticated && <Header />}
      <main className={isAuthenticated ? 'pt-20' : ''}>
        <Outlet />
      </main>
    </div>
  );
};

export const Router = () => {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-gray-600 dark:text-gray-400">Caricamento...</p>
      </div>
    );
  }

  return (
    <Routes>
      <Route element={<RootLayout />}>
        {isAuthenticated ? (
          <>
            <Route path="/" element={<HomePage />} />
            <Route path="/login" element={<Navigate to="/" replace />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </>
        ) : (
          <>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/" element={<Navigate to="/login" replace />} />
            <Route path="*" element={<Navigate to="/login" replace />} />
          </>
        )}
      </Route>
    </Routes>
  );
};
