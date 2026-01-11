import { Navigate } from 'react-router-dom';
import type { JSX, ReactElement } from 'react';

type PublicRouteProps = {
  isAuthenticated: boolean;
  children: ReactElement;
};

export function PublicRoute({ isAuthenticated, children }: PublicRouteProps): JSX.Element {
  if (isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  return children;
}
