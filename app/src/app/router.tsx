import { Routes, Route, Outlet, Navigate } from 'react-router-dom';
import { LoginPage } from '../features/auth/LoginPage';
import { HomePage } from '../features/entries/HomePage';
import { RequireAuth } from '../features/auth/RequireAuth';
import { ProtectLogin } from '../features/auth/ProtectLogin';
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

export const Router = () => (
  <Routes>
    <Route element={<RootLayout />}>
      <Route
        path="/login"
        element={
          <ProtectLogin>
            <LoginPage />
          </ProtectLogin>
        }
      />
      <Route
        path="/"
        element={
          <RequireAuth>
            <HomePage />
          </RequireAuth>
        }
      />
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Route>
  </Routes>
);
