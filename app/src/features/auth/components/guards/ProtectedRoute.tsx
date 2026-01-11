import { Navigate } from 'react-router-dom';
import type { JSX, ReactElement } from 'react';

type ProtectedRouteProps = {
  isAuthenticated: boolean;
  children: ReactElement;
};

export function ProtectedRoute({ isAuthenticated, children }: ProtectedRouteProps): JSX.Element {
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return children;
}
