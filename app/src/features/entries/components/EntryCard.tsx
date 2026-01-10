import type { Entry } from '../../../shared/api/types';

interface EntryCardProps {
  entry: Entry;
  onDelete: (id: string) => void;
}

export const EntryCard = ({ entry, onDelete }: EntryCardProps) => {
  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('it-IT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  return (
    <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-4 hover:shadow-lg transition">
      <p className="text-gray-600 dark:text-gray-400 mb-3 line-clamp-4 text-sm">
        {entry.text}
      </p>
      <div className="flex justify-between items-center">
        <span className="text-xs text-gray-500 dark:text-gray-400">
          {formatDate(entry.createdAt)}
        </span>
        <button
          onClick={() => onDelete(entry.id)}
          className="px-3 py-1 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded hover:opacity-80 transition text-sm font-medium"
        >
          Elimina
        </button>
      </div>
    </div>
  );
};
