import { AlertTriangle } from 'lucide-react';

interface Props {
  zScore: number;
}

export function AnomalyBadge({ zScore }: Props) {
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-red-100 px-2 py-0.5 text-xs font-medium text-red-700 dark:bg-red-900 dark:text-red-300">
      <AlertTriangle className="h-3 w-3" />
      {zScore.toFixed(1)}x
    </span>
  );
}
