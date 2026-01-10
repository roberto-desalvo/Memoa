import { type ReactNode } from 'react';

interface ProtectLoginProps {
  children: ReactNode;
}

export const ProtectLogin = ({ children }: ProtectLoginProps) => {
  // Router gestisce la protezione, questo componente è solo un wrapper
  return <>{children}</>;
};
