import { Outlet } from 'react-router-dom';
import { Header } from '../../shared/components/Header';
import type { JSX } from 'react';

type RootLayoutProps = {
  isAuthenticated: boolean;
};

export function RootLayout({ isAuthenticated }: RootLayoutProps): JSX.Element {
  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {isAuthenticated && <Header />}
      <main className={isAuthenticated ? 'pt-20' : ''}>
        <Outlet />
      </main>
    </div>
  );
}
