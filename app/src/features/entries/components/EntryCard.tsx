import { useEffect, useState, useCallback, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import type { Entry } from '../../../shared/types/types';
import { supabaseStorage } from '../../../shared/lib/supabaseStorage';

interface EntryCardProps {
  entry: Entry;
  onDelete: (id: string) => void;
}

const URL_EXPIRY_WARNING_THRESHOLD = 3600; // 1 ora prima della scadenza

export const EntryCard = ({ entry, onDelete }: EntryCardProps) => {
  const navigate = useNavigate();
  const [audioSignedUrl, setAudioSignedUrl] = useState<string | null>(null);
  const [audioLoading, setAudioLoading] = useState(false);
  const [audioError, setAudioError] = useState<string | null>(null);
  const [urlExpiresAt, setUrlExpiresAt] = useState<number | null>(null);
  const [isUrlExpiring, setIsUrlExpiring] = useState(false);
  const refreshTimeoutRef = useRef<NodeJS.Timeout | null>(null);

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
    const minutes = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return minutes > 0 ? `${minutes}m ${secs}s` : `${secs}s`;
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  };

  const loadSignedUrl = useCallback(async () => {
    if (!entry.audioPath) return;

    setAudioLoading(true);
    setAudioError(null);
    setIsUrlExpiring(false);

    try {
      const url = await supabaseStorage.getSignedUrl(entry.audioPath);
      const expiresAt = Date.now() + 86400 * 1000; // 24 ore

      setAudioSignedUrl(url);
      setUrlExpiresAt(expiresAt);
      setAudioLoading(false);

      // Schedulare refresh 1 ora prima della scadenza
      if (refreshTimeoutRef.current) {
        clearTimeout(refreshTimeoutRef.current);
      }
      const refreshIn = 86400 * 1000 - URL_EXPIRY_WARNING_THRESHOLD * 1000;
      refreshTimeoutRef.current = setTimeout(() => {
        setIsUrlExpiring(true);
      }, refreshIn);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore accesso audio';
      setAudioError(message);
      setAudioLoading(false);
    }
  }, [entry.audioPath]);

  // Genera signed URL al mount se c'è audio
  useEffect(() => {
    loadSignedUrl();

    return () => {
      if (refreshTimeoutRef.current) {
        clearTimeout(refreshTimeoutRef.current);
      }
    };
  }, [loadSignedUrl]);

  return (
    <div 
      onClick={() => navigate(`/entries/${entry.id}`)}
      className="bg-white dark:bg-gray-800 rounded-lg shadow p-4 hover:shadow-lg transition cursor-pointer"
    >
      <p className="text-gray-600 dark:text-gray-400 mb-3 line-clamp-4 text-sm">
        {entry.text}
      </p>

      {/* Audio Player */}
      {entry.audioPath && (
        <div className="mb-4 p-3 bg-blue-50 dark:bg-blue-900 rounded-lg border border-blue-200 dark:border-blue-700">
          {audioLoading && (
            <div className="flex items-center gap-2">
              <div className="animate-spin h-4 w-4 border-2 border-blue-600 dark:border-blue-200 border-t-transparent rounded-full"></div>
              <p className="text-xs text-blue-600 dark:text-blue-200">Caricamento audio...</p>
            </div>
          )}

          {audioError && (
            <div className="flex flex-col gap-2">
              <p className="text-xs text-red-600 dark:text-red-200">
                ⚠️ {audioError}
              </p>
              <button
                onClick={loadSignedUrl}
                className="text-xs text-blue-600 dark:text-blue-200 hover:underline font-medium"
              >
                Riprova caricamento
              </button>
            </div>
          )}

          {audioSignedUrl && !audioLoading && !audioError && (
            <>
              <audio
                controls
                src={audioSignedUrl}
                className="w-full h-8 mb-2"
                onError={() => {
                  setAudioError('Impossibile riprodurre il file audio');
                }}
              />
              
              {/* Audio Info */}
              <div className="grid grid-cols-2 gap-2 text-xs text-blue-700 dark:text-blue-300">
                {entry.audioDurationMs && (
                  <div>
                    <span className="font-semibold">Durata:</span> {formatDuration(entry.audioDurationMs)}
                  </div>
                )}
                {entry.audioSizeBytes && (
                  <div>
                    <span className="font-semibold">Dimensione:</span> {formatFileSize(entry.audioSizeBytes)}
                  </div>
                )}
                {entry.audioMime && (
                  <div className="col-span-2">
                    <span className="font-semibold">Tipo:</span> {entry.audioMime}
                  </div>
                )}
              </div>

              {/* URL Expiry Warning */}
              {isUrlExpiring && (
                <div className="mt-2 p-2 bg-yellow-100 dark:bg-yellow-900 border border-yellow-300 dark:border-yellow-700 rounded text-xs text-yellow-800 dark:text-yellow-200">
                  <p>⏰ Link in scadenza. Ricarica la pagina per rinnovare.</p>
                </div>
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
          onClick={(e) => {
            e.stopPropagation();
            onDelete(entry.id);
          }}
          className="px-3 py-1 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded hover:opacity-80 transition text-sm font-medium"
        >
          Elimina
        </button>
      </div>
    </div>
  );
};
