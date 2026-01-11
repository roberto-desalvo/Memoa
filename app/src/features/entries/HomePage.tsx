import { useCallback } from 'react';
import { useEntries } from '../../shared/hooks/useEntries';
import { useUser } from '../../shared/hooks/useUser';
import { useEntryForm } from '../../shared/hooks/useEntryForm';
import { UserInfo } from './components/UserInfo';
import { UserLoadingState } from './components/UserLoadingState';
import { EntryForm } from './components/EntryForm';
import { EntryList } from './components/EntryList';
import { ErrorMessage } from './components/ErrorMessage';

export const HomePage = () => {
  const { entries, loading, error, deleteEntry } = useEntries();
  const { user, loading: userLoading } = useUser();
  const form = useEntryForm();

  const handleAddEntry = useCallback(
    async (e: React.FormEvent<HTMLFormElement>) => {
      e.preventDefault();
      form.setValidationError(null);

      if (!form.validate()) {
        return;
      }

      const success = await form.submitEntry();
      if (success) {
        // Ricarica entries dopo il submit
        window.location.reload();
      }
    },
    [form]
  );

  const handleDeleteEntry = useCallback(
    async (id: string) => {
      await deleteEntry(id);
    },
    [deleteEntry]
  );

  const handleAudioBlob = useCallback((blob: Blob | null, metadata: any) => {
    form.setAudioBlob(blob, metadata);
  }, [form]);

  const handleAudioClear = useCallback(() => {
    form.clearAudio();
  }, [form]);

  return (
    <main className="max-w-6xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
      {/* User Info Section */}
      {userLoading && <UserLoadingState />}
      {user && <UserInfo email={user.email} id={user.id} />}

      {/* Entry Form Section */}
      <EntryForm
        content={form.content}
        onContentChange={form.setContent}
        validationError={form.validationError}
        onSubmit={handleAddEntry}
        onAudioBlob={handleAudioBlob}
        onAudioClear={handleAudioClear}
        hasAudio={form.audioBlob !== null}
        isSubmitting={form.isSubmitting}
        submitError={form.submitError}
      />

      {/* Error Message */}
      {error && <ErrorMessage message={error} />}

      {/* Entries List */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        <EntryList
          entries={entries}
          loading={loading}
          onDeleteEntry={handleDeleteEntry}
        />
      </div>
    </main>
  );
};
