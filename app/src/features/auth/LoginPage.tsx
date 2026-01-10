import { useEffect, useRef } from 'react';
import { useAuthLogic } from '../../shared/hooks/useAuthLogic';
import { LoginForm } from './components/LoginForm';
import { LinkSentMessage } from './components/LinkSentMessage';

export const LoginPage = () => {
  const { email, setEmail, loading, error, linkSent, sendMagicLink, resetForm } =
    useAuthLogic();
  
  // Previeni accidental refresh al primo click subito dopo il mount
  const hasInitialized = useRef(false);
  
  useEffect(() => {
    // Log che LoginPage è montata
    console.log('[LoginPage] Mounted');
    hasInitialized.current = true;
    
    return () => {
      console.log('[LoginPage] Unmounted');
    };
  }, []);

  return (
    <div className="flex items-center justify-center min-h-screen bg-gray-50 dark:bg-gray-900">
      <div className="w-full max-w-md p-8 bg-white dark:bg-gray-800 rounded-lg shadow-md">
        <h1 className="text-2xl font-bold text-center text-gray-800 dark:text-white mb-6">
          Accedi
        </h1>

        {!linkSent ? (
          <LoginForm
            email={email}
            onEmailChange={setEmail}
            loading={loading}
            error={error}
            onSubmit={sendMagicLink}
          />
        ) : (
          <LinkSentMessage email={email} onTryAgain={resetForm} />
        )}
      </div>
    </div>
  );
};
