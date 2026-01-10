import type { Entry } from '../../../shared/api/types';
import { EntryCard } from './EntryCard';

interface EntryListProps {
  entries: Entry[];
  loading: boolean;
  onDeleteEntry: (id: string) => void;
}

export const EntryList = ({
  entries,
  loading,
  onDeleteEntry,
}: EntryListProps) => {
  if (loading) {
    return (
      <div className="col-span-full text-center py-8">
        <p className="text-gray-500 dark:text-gray-400 text-lg">
          Caricamento...
        </p>
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <div className="col-span-full text-center py-8">
        <p className="text-gray-500 dark:text-gray-400 text-lg">
          Nessuna nota ancora. Aggiungi la tua prima nota!
        </p>
      </div>
    );
  }

  return (
    <>
      {entries.map((entry) => (
        <EntryCard
          key={entry.id}
          entry={entry}
          onDelete={onDeleteEntry}
        />
      ))}
    </>
  );
};
