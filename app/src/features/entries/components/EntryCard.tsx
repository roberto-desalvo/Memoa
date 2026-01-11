import { useEffect, useState } from 'react';
import type { Entry } from '../../../shared/types/types';
import { supabaseStorage } from '../../../shared/lib/supabaseStorage';

interface EntryCardProps {
  entry: Entry;
  onDelete: (id: string) => void;
}

export const EntryCard = ({ entry, onDelete }: EntryCardProps) => {
  const [audioSignedUrl, setAudioSignedUrl] = useState<string | null>(null);
  const [audioLoading, setAudioLoading] = useState(false);
  const [audioError, setAudioError] = useState<string | null>(null);

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('it-IT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const formatDuration = (durationMs: number) => {
    const seconds = Math.round(durationMs / 1000);
    return `${seconds}s`;
  };

  // Genera signed URL al mount se c'è audio
  useEffect(() => {
    if (entry.audioPath) {
      setAudioLoading(true);
      setAudioError(null);

      supabaseStorage
        .getSignedUrl(entry.audioPath)
        .then((url) => {
          setAudioSignedUrl(url);
          setAudioLoading(false);
        })
        .catch((err) => {
          const message = err instanceof Error ? err.message : 'Errore accesso audio';
          setAudioError(message);
          setAudioLoading(false);
        });
    }
  }, [entry.audioPath]);

  return (
    <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-4 hover:shadow-lg transition">
      <p className="text-gray-600 dark:text-gray-400 mb-3 line-clamp-4 text-sm">
        {entry.text}
      </p>

      {/* Audio Player */}
      {entry.audioPath && (
        <div className="mb-4 p-3 bg-blue-50 dark:bg-blue-900 rounded-lg">
          {audioLoading && (
            <p className="text-xs text-blue-600 dark:text-blue-200">Caricamento audio...</p>
          )}

          {audioError && (
            <p className="text-xs text-red-600 dark:text-red-200">{audioError}</p>
          )}

          {audioSignedUrl && !audioLoading && (
            <>
              <audio
                controls
                src={audioSignedUrl}
                className="w-full h-8 mb-2"
              />
              {entry.audioDurationMs && (
                <p className="text-xs text-blue-600 dark:text-blue-200">
                  Durata: {formatDuration(entry.audioDurationMs)}
                </p>
              )}
            </>
          )}
        </div>
      )}

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
