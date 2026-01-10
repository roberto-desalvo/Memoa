interface EntryFormProps {
  content: string;
  onContentChange: (value: string) => void;
  validationError: string | null;
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
}

export const EntryForm = ({
  content,
  onContentChange,
  validationError,
  onSubmit,
}: EntryFormProps) => {
  return (
    <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 mb-6">
      <h2 className="text-xl font-semibold text-gray-800 dark:text-white mb-4">
        Aggiungi una nota
      </h2>

      {validationError && (
        <div className="mb-4 p-3 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded-lg text-sm">
          {validationError}
        </div>
      )}

      <form onSubmit={onSubmit} className="space-y-4">
        <textarea
          value={content}
          onChange={(e) => onContentChange(e.target.value)}
          placeholder="Scrivi la tua nota..."
          rows={4}
          className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-transparent outline-none bg-white dark:bg-gray-700 text-gray-800 dark:text-white"
        />
        <button
          type="submit"
          className="w-full py-2 px-4 bg-indigo-600 dark:bg-indigo-700 text-white font-medium rounded-lg hover:opacity-90 transition"
        >
          Aggiungi Nota
        </button>
      </form>
    </div>
  );
};
