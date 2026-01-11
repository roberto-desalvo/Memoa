import { useAudioRecorder } from '../hooks/useAudioRecorder';
import type { AudioMetadata } from '../hooks/useAudioRecorder';
import { Spinner } from '../../../shared/components/Spinner';

interface EntryFormProps {
  content: string;
  onContentChange: (value: string) => void;
  validationError: string | null;
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
  onAudioBlob?: (blob: Blob | null, metadata: AudioMetadata | null) => void;
  onAudioClear?: () => void;
  hasAudio?: boolean;
  isSubmitting?: boolean;
  submitError?: string | null;
}

export const EntryForm = ({
  content,
  onContentChange,
  validationError,
  onSubmit,
  onAudioBlob,
  onAudioClear,
  hasAudio = false,
  isSubmitting = false,
  submitError = null,
}: EntryFormProps) => {
  const { isRecording, elapsedSeconds, blob, audioMetadata, error: recordingError, startRecording, stopRecording, resetRecording } = useAudioRecorder();

  const handleAudioRecording = () => {
    if (isRecording) {
      stopRecording((audioBlob: Blob, audioMetadataData: AudioMetadata) => {
        if (onAudioBlob) {
          onAudioBlob(audioBlob, audioMetadataData);
        }
      });
    } else {
      resetRecording();
      startRecording();
    }
  };

  const handleClearAudio = () => {
    resetRecording();
    if (onAudioClear) {
      onAudioClear();
    }
  };

  const maxSeconds = 300;
  const progressPercent = (elapsedSeconds / maxSeconds) * 100;
  const audioDurationDisplay = `${elapsedSeconds}s`;

  const isDisabled = isRecording || isSubmitting;

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

      {recordingError && (
        <div className="mb-4 p-3 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded-lg text-sm">
          {recordingError}
        </div>
      )}

      {submitError && (
        <div className="mb-4 p-3 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded-lg text-sm">
          {submitError}
        </div>
      )}

      <form onSubmit={onSubmit} className="space-y-4">
        <textarea
          value={content}
          onChange={(e) => onContentChange(e.target.value)}
          placeholder="Scrivi la tua nota..."
          rows={4}
          disabled={isDisabled}
          className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-transparent outline-none bg-white dark:bg-gray-700 text-gray-800 dark:text-white disabled:opacity-50"
        />

        {/* Audio Recording Section */}
        <div className="space-y-3 p-4 bg-gray-50 dark:bg-gray-700 rounded-lg">
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={handleAudioRecording}
              disabled={isSubmitting}
              className={`flex-1 py-2 px-4 font-medium rounded-lg transition ${
                isRecording
                  ? 'bg-red-600 hover:opacity-90 text-white'
                  : 'bg-gray-200 dark:bg-gray-600 hover:opacity-90 text-gray-800 dark:text-white'
              } disabled:opacity-50`}
            >
              {isRecording ? `⏹️ Stop (${audioDurationDisplay} / ${maxSeconds}s)` : '🎙️ Registra Audio'}
            </button>
          </div>

          {/* Recording Progress Bar */}
          {isRecording && (
            <div className="w-full bg-gray-300 dark:bg-gray-600 rounded-full h-2">
              <div
                className="bg-red-600 h-2 rounded-full transition-all duration-100"
                style={{ width: `${progressPercent}%` }}
              />
            </div>
          )}

          {/* Audio Badge */}
          {(hasAudio || blob) && !isRecording && (
            <div className="flex items-center justify-between bg-green-100 dark:bg-green-900 p-3 rounded-lg">
              <span className="text-sm text-green-700 dark:text-green-100">
                ✓ Audio registrato: {audioMetadata?.durationMs ? Math.round(audioMetadata.durationMs / 1000) : 0}s
              </span>
              <button
                type="button"
                onClick={handleClearAudio}
                disabled={isSubmitting}
                className="text-sm px-3 py-1 bg-red-500 hover:opacity-90 text-white rounded disabled:opacity-50"
              >
                ❌ Scarta
              </button>
            </div>
          )}
        </div>

        <button
          type="submit"
          disabled={isDisabled}
          className="w-full py-2 px-4 bg-indigo-600 dark:bg-indigo-700 text-white font-medium rounded-lg hover:opacity-90 transition disabled:opacity-50 flex items-center justify-center gap-2"
        >
          {isSubmitting && <Spinner label="" />}
          <span>{isSubmitting ? 'Caricamento nota...' : 'Aggiungi Nota'}</span>
        </button>
      </form>
    </div>
  );
};
