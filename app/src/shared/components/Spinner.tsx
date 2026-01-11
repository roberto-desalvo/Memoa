import type { JSX } from 'react';

export function Spinner({ label = 'Caricamento...' }: { label?: string }): JSX.Element {
  return (
    <div className="flex items-center gap-2" role="status" aria-busy="true" aria-live="polite">
      <span className="inline-block h-5 w-5 rounded-full border-2 border-gray-300 border-t-transparent animate-spin" />
      <span className="text-gray-600 dark:text-gray-400">{label}</span>
    </div>
  );
}
