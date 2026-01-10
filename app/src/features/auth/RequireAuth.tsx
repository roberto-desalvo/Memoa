import { type ReactNode } from 'react';

interface RequireAuthProps {
  children: ReactNode;
}

export const RequireAuth = ({ children }: RequireAuthProps) => {
  // Router gestisce la protezione, questo componente è solo un wrapper
  return <>{children}</>;
};
