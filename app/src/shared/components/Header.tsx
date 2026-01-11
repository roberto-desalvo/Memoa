import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../features/auth/AuthProvider';
import { useTheme } from '../../app/providers/ThemeProvider';
import { MdDarkMode, MdLightMode } from 'react-icons/md';

export const Header = () => {
  const navigate = useNavigate();
  const { logout } = useAuth();
  const { theme, toggleTheme } = useTheme();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const handleHome = () => {
    navigate('/');
  };

  return (
    <header className="fixed top-0 w-full bg-indigo-300 dark:bg-indigo-600 shadow-md z-50">
      <div className="max-w-6xl mx-auto px-4 py-4 flex justify-between items-center">
        <h1 className="text-2xl font-bold text-gray-800 dark:text-white">Memoa</h1>
        <nav className="flex gap-4 items-center">
          <button
            onClick={toggleTheme}
            className="p-2 rounded-lg bg-white dark:bg-gray-800 text-gray-800 dark:text-white hover:opacity-80 transition"
            title={theme === 'light' ? 'Tema scuro' : 'Tema chiaro'}
          >
            {theme === 'light' ? <MdDarkMode size={24} /> : <MdLightMode size={24} />}
          </button>
          <button
            onClick={handleHome}
            className="px-4 py-2 bg-indigo-400 dark:bg-indigo-700 text-gray-800 dark:text-white rounded-lg hover:opacity-80 transition font-medium"
          >
            Home
          </button>
          <button
            onClick={handleLogout}
            className="px-4 py-2 bg-red-300 dark:bg-red-600 text-gray-800 dark:text-white rounded-lg hover:opacity-80 transition font-medium"
          >
            Logout
          </button>
        </nav>
      </div>
    </header>
  );
};
