interface LoginFormProps {
  email: string;
  onEmailChange: (email: string) => void;
  loading: boolean;
  error: string;
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
}

export const LoginForm = ({
  email,
  onEmailChange,
  loading,
  error,
  onSubmit,
}: LoginFormProps) => {
  const handleEmailChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    e.preventDefault();
    e.stopPropagation();
    onEmailChange(e.target.value);
  };

  return (
    <form onSubmit={onSubmit} className="space-y-4">
      <div>
        <label
          htmlFor="email"
          className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1"
        >
          Email
        </label>
        <input
          id="email"
          type="email"
          value={email}
          onChange={handleEmailChange}
          onFocus={(e) => e.currentTarget.select()}
          placeholder="tuo@email.com"
          required
          disabled={loading}
          className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-transparent outline-none transition bg-white dark:bg-gray-700 text-gray-800 dark:text-white disabled:opacity-50"
        />
      </div>

      {error && (
        <div className="p-3 bg-red-100 dark:bg-red-900 border border-red-300 dark:border-red-700 text-red-700 dark:text-red-100 rounded">
          {error}
        </div>
      )}

      <button
        type="submit"
        disabled={loading}
        className="w-full py-2 px-4 bg-indigo-600 dark:bg-indigo-700 text-white font-medium rounded-lg hover:opacity-90 disabled:bg-gray-400 transition"
      >
        {loading ? 'Invio in corso...' : 'Invia Magic Link'}
      </button>
    </form>
  );
};
