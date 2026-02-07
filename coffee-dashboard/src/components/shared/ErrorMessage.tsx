import { AlertTriangle } from 'lucide-react';

interface Props {
  message?: string;
}

export function ErrorMessage({ message = 'Daten konnten nicht geladen werden.' }: Props) {
  return (
    <div className="flex items-center gap-2 rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-red-800 dark:border-red-700 dark:bg-red-950 dark:text-red-200">
      <AlertTriangle className="h-5 w-5 shrink-0" />
      <span>{message}</span>
    </div>
  );
}
