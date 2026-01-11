import { useEffect, useState, useCallback, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import type { Entry } from '../../shared/types/types';
import { entriesService } from './services/entriesService';
import { supabaseStorage } from '../../shared/lib/supabaseStorage';
import { Spinner } from '../../shared/components/Spinner';
import { useEditEntry } from './hooks/useEditEntry';

const URL_EXPIRY_WARNING_THRESHOLD = 3600; // 1 ora prima della scadenza

export const EntryDetailPage = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  
  const [entry, setEntry] = useState<Entry | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [audioSignedUrl, setAudioSignedUrl] = useState<string | null>(null);
  const [audioLoading, setAudioLoading] = useState(false);
  const [audioError, setAudioError] = useState<string | null>(null);
  const [isUrlExpiring, setIsUrlExpiring] = useState(false);
  const refreshTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  // New audio preview signed URL
  const [newAudioSignedUrl, setNewAudioSignedUrl] = useState<string | null>(null);

  // Edit functionality
  const editEntry = useEditEntry(entry || { id: '', text: '', createdAt: '' } as Entry, (updatedEntry) => {
    setEntry(updatedEntry);
  });

  // Carica i dati della nota
  useEffect(() => {
    const loadEntry = async () => {
      if (!id) {
        setError('ID nota non valido');
        setLoading(false);
        return;
      }

      try {
        const response = await entriesService.getEntryById(id);
        
        if (!response.success || !response.data) {
          setError(response.error || 'Nota non trovata');
          setLoading(false);
          return;
        }

        setEntry(response.data);
        setError(null);
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Errore nel caricamento della nota';
        setError(message);
      } finally {
        setLoading(false);
      }
    };

    loadEntry();
  }, [id]);

  // Carica il signed URL per l'audio
  const loadSignedUrl = useCallback(async () => {
    if (!entry?.audioPath) return;

    setAudioLoading(true);
    setAudioError(null);
    setIsUrlExpiring(false);

    try {
      const url = await supabaseStorage.getSignedUrl(entry.audioPath);
      setAudioSignedUrl(url);
      setAudioLoading(false);

      // Schedulare refresh 1 ora prima della scadenza (24 ore)
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
  }, [entry?.audioPath]);

  useEffect(() => {
    if (entry?.audioPath) {
      loadSignedUrl();
    }

    return () => {
      if (refreshTimeoutRef.current) {
        clearTimeout(refreshTimeoutRef.current);
      }
    };
  }, [entry?.audioPath, loadSignedUrl]);

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

  // Generate signed URL for new audio blob preview
  useEffect(() => {
    if (editEntry.newAudioBlob) {
      const url = URL.createObjectURL(editEntry.newAudioBlob);
      setNewAudioSignedUrl(url);
      return () => URL.revokeObjectURL(url);
    } else {
      setNewAudioSignedUrl(null);
    }
  }, [editEntry.newAudioBlob]);

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Spinner />
      </div>
    );
  }

  if (error || !entry) {
    return (
      <div className="min-h-screen bg-white dark:bg-gray-900">
        <div className="max-w-3xl mx-auto px-4 py-8">
          <button
            onClick={() => navigate('/')}
            className="mb-6 flex items-center gap-2 text-blue-600 dark:text-blue-400 hover:underline font-medium"
          >
            ← Torna all'elenco
          </button>
          <div className="bg-red-50 dark:bg-red-900 border border-red-200 dark:border-red-700 rounded-lg p-4">
            <p className="text-red-600 dark:text-red-100">{error || 'Nota non trovata'}</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-white dark:bg-gray-900">
      <div className="max-w-3xl mx-auto px-4 py-8">
        {/* Back Button */}
        <button
          onClick={() => navigate('/')}
          className="mb-6 flex items-center gap-2 text-blue-600 dark:text-blue-400 hover:underline font-medium"
        >
          ← Torna all'elenco
        </button>

        {/* Header */}
        <div className="mb-8">
          <div className="flex justify-between items-start mb-4">
            <div>
              {entry.updatedAt ? (
                <div className="flex gap-4 text-sm text-gray-600 dark:text-gray-400">
                  <span>Creata: {formatDate(entry.createdAt)}</span>
                  <span>Modificata: {formatDate(entry.updatedAt)}</span>
                </div>
              ) : (
                <p className="text-sm text-gray-600 dark:text-gray-400">
                  Creata: {formatDate(entry.createdAt)}
                </p>
              )}
            </div>
            
            {/* Edit/Save/Cancel Buttons */}
            {!editEntry.isEditMode ? (
              <button
                onClick={editEntry.enterEditMode}
                className="px-4 py-2 bg-blue-600 dark:bg-blue-700 text-white rounded hover:bg-blue-700 dark:hover:bg-blue-800 transition font-medium flex items-center gap-2"
              >
                ✏️ Modifica
              </button>
            ) : (
              <div className="flex gap-2">
                <button
                  onClick={editEntry.saveChanges}
                  disabled={!editEntry.isValid || editEntry.isSaving}
                  className="px-4 py-2 bg-green-600 dark:bg-green-700 text-white rounded hover:bg-green-700 dark:hover:bg-green-800 transition font-medium disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
                >
                  {editEntry.isSaving ? (
                    <>
                      <div className="animate-spin h-4 w-4 border-2 border-white border-t-transparent rounded-full"></div>
                      Salvataggio...
                    </>
                  ) : (
                    <>💾 Salva</>
                  )}
                </button>
                <button
                  onClick={editEntry.cancelEdit}
                  disabled={editEntry.isSaving}
                  className="px-4 py-2 bg-gray-600 dark:bg-gray-700 text-white rounded hover:bg-gray-700 dark:hover:bg-gray-800 transition font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  ❌ Annulla
                </button>
              </div>
            )}
          </div>

          {/* Error Message */}
          {editEntry.error && (
            <div className="mt-4 p-3 bg-red-50 dark:bg-red-900 border border-red-200 dark:border-red-700 rounded text-red-600 dark:text-red-100">
              {editEntry.error}
            </div>
          )}
        </div>

        {/* Full Text */}
        <div className="mb-8 p-6 bg-gray-50 dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700">
          <h2 className="text-sm font-semibold text-gray-600 dark:text-gray-400 mb-4 uppercase tracking-wide">
            Testo completo
          </h2>
          
          {!editEntry.isEditMode ? (
            <p className="text-gray-800 dark:text-gray-100 leading-relaxed whitespace-pre-wrap">
              {entry.text}
            </p>
          ) : (
            <div>
              <textarea
                value={editEntry.editedText}
                onChange={(e) => editEntry.setEditedText(e.target.value)}
                className="w-full p-3 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-900 text-gray-800 dark:text-gray-100 focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-none"
                rows={10}
                placeholder="Inserisci il testo della nota..."
              />
              <div className="flex justify-between mt-2 text-sm">
                <span className={`${editEntry.textError ? 'text-red-600 dark:text-red-400' : 'text-gray-500 dark:text-gray-400'}`}>
                  {editEntry.textError || `${editEntry.editedText.length.toLocaleString()} / 100.000 caratteri`}
                </span>
              </div>
            </div>
          )}
        </div>

        {/* Audio Section */}
        {(entry.audioPath || editEntry.isEditMode) && (
          <div className="mb-8 p-6 bg-blue-50 dark:bg-blue-900 rounded-lg border border-blue-200 dark:border-blue-700">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-sm font-semibold text-blue-700 dark:text-blue-300 uppercase tracking-wide">
                Audio
              </h2>
              
              {/* Replace Audio Button in Edit Mode */}
              {editEntry.isEditMode && !editEntry.audioRecorder.isRecording && (
                <button
                  onClick={() => {
                    if (editEntry.audioRecorder.blob) {
                      editEntry.audioRecorder.resetRecording();
                    } else {
                      editEntry.audioRecorder.startRecording();
                    }
                  }}
                  className="px-3 py-1 bg-blue-600 dark:bg-blue-700 text-white rounded hover:opacity-90 transition font-medium text-sm"
                >
                  {entry.audioPath ? '🔄 Sostituisci audio' : '➕ Aggiungi audio'}
                </button>
              )}
            </div>

            {/* Audio Recorder in Edit Mode */}
            {editEntry.isEditMode && editEntry.audioRecorder.isRecording && (
              <div className="mb-4 p-4 bg-white dark:bg-gray-800 rounded border border-blue-200 dark:border-blue-700">
                <div className="flex items-center justify-between mb-3">
                  <div className="flex items-center gap-3">
                    <div className="w-3 h-3 bg-red-600 rounded-full animate-pulse"></div>
                    <span className="text-red-600 dark:text-red-400 font-semibold">
                      Registrazione in corso... {editEntry.audioRecorder.elapsedSeconds}s
                    </span>
                  </div>
                  <button
                    onClick={() => {
                      editEntry.audioRecorder.stopRecording(editEntry.handleAudioRecorded);
                    }}
                    className="px-3 py-1 bg-red-600 text-white rounded hover:opacity-90 transition font-medium text-sm"
                  >
                    ⏹️ Stop
                  </button>
                </div>
                <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2">
                  <div
                    className="bg-red-600 h-2 rounded-full transition-all duration-300"
                    style={{ width: `${(editEntry.audioRecorder.elapsedSeconds / 300) * 100}%` }}
                  ></div>
                </div>
              </div>
            )}

            {/* Audio Upload Progress */}
            {editEntry.isUploadingAudio && (
              <div className="mb-4 p-3 bg-white dark:bg-gray-800 rounded border border-blue-200 dark:border-blue-700">
                <div className="flex items-center gap-3">
                  <div className="animate-spin h-5 w-5 border-2 border-blue-600 dark:border-blue-300 border-t-transparent rounded-full"></div>
                  <p className="text-blue-600 dark:text-blue-200">Caricamento nuovo audio...</p>
                </div>
              </div>
            )}

            {/* Upload Error */}
            {editEntry.uploadError && (
              <div className="mb-4 p-3 bg-red-50 dark:bg-red-900 border border-red-200 dark:border-red-700 rounded text-red-600 dark:text-red-100">
                ⚠️ {editEntry.uploadError}
              </div>
            )}

            {/* Original Audio */}
            {entry.audioPath && (
              <div className="mb-4">
                <p className="text-xs font-semibold text-blue-600 dark:text-blue-300 mb-2">
                  Audio {editEntry.newAudioPath ? 'originale' : 'corrente'}
                </p>
                
                {audioLoading && (
                  <div className="flex items-center gap-3">
                    <div className="animate-spin h-5 w-5 border-2 border-blue-600 dark:border-blue-300 border-t-transparent rounded-full"></div>
                    <p className="text-blue-600 dark:text-blue-200">Caricamento audio...</p>
                  </div>
                )}

                {audioError && (
                  <div className="flex flex-col gap-3">
                    <p className="text-red-600 dark:text-red-200">
                      ⚠️ {audioError}
                    </p>
                    <button
                      onClick={() => loadSignedUrl()}
                      className="w-fit px-3 py-2 bg-blue-600 dark:bg-blue-700 text-white rounded hover:opacity-90 transition font-medium text-sm"
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
                      className="w-full mb-2"
                      onError={() => {
                        setAudioError('Impossibile riprodurre il file audio');
                      }}
                    />

                    {/* Audio Info Grid */}
                    <div className="grid grid-cols-2 md:grid-cols-3 gap-4 p-4 bg-white dark:bg-gray-800 rounded border border-blue-100 dark:border-blue-800">
                      {entry.audioDurationMs && (
                        <div>
                          <p className="text-xs font-semibold text-blue-600 dark:text-blue-300">Durata</p>
                          <p className="text-blue-800 dark:text-blue-200">
                            {formatDuration(entry.audioDurationMs)}
                          </p>
                        </div>
                      )}
                      {entry.audioSizeBytes && (
                        <div>
                          <p className="text-xs font-semibold text-blue-600 dark:text-blue-300">Dimensione</p>
                          <p className="text-blue-800 dark:text-blue-200">
                            {formatFileSize(entry.audioSizeBytes)}
                          </p>
                        </div>
                      )}
                      {entry.audioMime && (
                        <div>
                          <p className="text-xs font-semibold text-blue-600 dark:text-blue-300">Tipo</p>
                          <p className="text-blue-800 dark:text-blue-200 text-sm">
                            {entry.audioMime}
                          </p>
                        </div>
                      )}
                    </div>

                    {/* URL Expiry Warning */}
                    {isUrlExpiring && (
                      <div className="mt-4 p-3 bg-yellow-100 dark:bg-yellow-900 border border-yellow-300 dark:border-yellow-700 rounded text-yellow-800 dark:text-yellow-200 text-sm">
                        <p>⏰ Link in scadenza. Ricarica la pagina per rinnovare.</p>
                      </div>
                    )}
                  </>
                )}
              </div>
            )}

            {/* New Audio Preview */}
            {editEntry.newAudioPath && newAudioSignedUrl && editEntry.newAudioMetadata && (
              <div className="p-4 bg-green-50 dark:bg-green-900 rounded border border-green-200 dark:border-green-700">
                <div className="flex justify-between items-center mb-2">
                  <p className="text-xs font-semibold text-green-600 dark:text-green-300">
                    Nuovo audio (sarà salvato)
                  </p>
                  <button
                    onClick={editEntry.discardNewAudio}
                    disabled={editEntry.isSaving}
                    className="px-2 py-1 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded hover:opacity-80 transition text-xs font-medium disabled:opacity-50"
                  >
                    🗑️ Scarta
                  </button>
                </div>
                <audio
                  controls
                  src={newAudioSignedUrl}
                  className="w-full mb-2"
                />
                <div className="grid grid-cols-2 md:grid-cols-3 gap-4 p-3 bg-white dark:bg-gray-800 rounded border border-green-100 dark:border-green-800">
                  <div>
                    <p className="text-xs font-semibold text-green-600 dark:text-green-300">Durata</p>
                    <p className="text-green-800 dark:text-green-200">
                      {formatDuration(editEntry.newAudioMetadata.durationMs)}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold text-green-600 dark:text-green-300">Dimensione</p>
                    <p className="text-green-800 dark:text-green-200">
                      {formatFileSize(editEntry.newAudioMetadata.sizeBytes)}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold text-green-600 dark:text-green-300">Tipo</p>
                    <p className="text-green-800 dark:text-green-200 text-sm">
                      {editEntry.newAudioMetadata.mimeType}
                    </p>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Audio Transcription Section */}
        {(entry.audioTranscription || editEntry.isEditMode) && (
          <div className="mb-8 p-6 bg-purple-50 dark:bg-purple-900 rounded-lg border border-purple-200 dark:border-purple-700">
            <h2 className="text-sm font-semibold text-purple-700 dark:text-purple-300 mb-4 uppercase tracking-wide">
              Trascrizione audio
            </h2>
            
            {!editEntry.isEditMode ? (
              <p className="text-purple-800 dark:text-purple-100 leading-relaxed whitespace-pre-wrap text-sm">
                {entry.audioTranscription}
              </p>
            ) : (
              <div>
                <textarea
                  value={editEntry.editedTranscription}
                  onChange={(e) => editEntry.setEditedTranscription(e.target.value)}
                  className="w-full p-3 border border-purple-300 dark:border-purple-600 rounded-lg bg-white dark:bg-gray-900 text-purple-800 dark:text-purple-100 focus:ring-2 focus:ring-purple-500 focus:border-transparent resize-none"
                  rows={5}
                  placeholder="Trascrizione audio (opzionale)..."
                />
                <div className="flex justify-between mt-2 text-sm">
                  <span className={`${editEntry.transcriptionError ? 'text-red-600 dark:text-red-400' : 'text-purple-600 dark:text-purple-400'}`}>
                    {editEntry.transcriptionError || `${editEntry.editedTranscription.length.toLocaleString()} / 100.000 caratteri`}
                  </span>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
