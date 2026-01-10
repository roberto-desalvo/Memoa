/* eslint-disable @typescript-eslint/no-unused-vars */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useEntries } from '../../shared/hooks/useEntries';

export const HomePage = () => {
  const navigate = useNavigate();
  const { entries, loading, error, addEntry, deleteEntry } = useEntries();

  const [newTitle, setNewTitle] = useState('');
  const [newContent, setNewContent] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleAddEntry = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setValidationError(null);

    if (!newTitle.trim()) {
      setValidationError('Il titolo è obbligatorio');
      return;
    }

    if (!newContent.trim()) {
      setValidationError('Il contenuto della nota è obbligatorio');
      return;
    }

    const success = await addEntry(newTitle, newContent);
    if (success) {
      setNewTitle('');
      setNewContent('');
    }
  };

  const handleDeleteEntry = async (id: string) => {
    await deleteEntry(id);
  };

  return (
    <div>
      {/* Main Content */}
      <main className="max-w-6xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
        {/* Add Entry Form */}
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 mb-6">
          <h2 className="text-xl font-semibold text-gray-800 dark:text-white mb-4">Aggiungi una nota</h2>
          
          {/* Validation Error */}
          {validationError && (
            <div className="mb-4 p-3 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded-lg text-sm">
              {validationError}
            </div>
          )}
          
          <form onSubmit={handleAddEntry} className="space-y-4">
            <input
              type="text"
              value={newTitle}
              onChange={(e) => setNewTitle(e.target.value)}
              placeholder="Titolo della nota"
              className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-transparent outline-none bg-white dark:bg-gray-700 text-gray-800 dark:text-white"
            />
            <textarea
              value={newContent}
              onChange={(e) => setNewContent(e.target.value)}
              placeholder="Contenuto della nota"
              rows={3}
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

        {/* Error Message */}
        {error && (
          <div className="col-span-full mb-6 p-4 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded-lg">
            {error}
          </div>
        )}

        {/* Entries List */}
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {loading ? (
            <div className="col-span-full text-center py-8">
              <p className="text-gray-500 dark:text-gray-400 text-lg">Caricamento...</p>
            </div>
          ) : entries.length > 0 ? (
            entries.map((entry) => (
              <div key={entry.id} className="bg-white dark:bg-gray-800 rounded-lg shadow p-4 hover:shadow-lg transition">
                <h3 className="text-lg font-semibold text-gray-800 dark:text-white mb-2">{entry.title}</h3>
                <p className="text-gray-600 dark:text-gray-400 mb-3 line-clamp-3 text-sm">{entry.content}</p>
                <div className="flex justify-between items-center">
                  <span className="text-xs text-gray-500 dark:text-gray-400">{entry.date}</span>
                  <button
                    onClick={() => handleDeleteEntry(entry.id)}
                    className="px-3 py-1 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded hover:opacity-80 transition text-sm font-medium"
                  >
                    Elimina
                  </button>
                </div>
              </div>
            ))
          ) : (
            <div className="col-span-full text-center py-8">
              <p className="text-gray-500 dark:text-gray-400 text-lg">Nessuna nota ancora. Aggiungi la tua prima nota!</p>
            </div>
          )}
        </div>
      </main>
    </div>
  );
};
