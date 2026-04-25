import { useState, useEffect } from 'react';
import { X } from 'lucide-react';
import { useAddExcludedDay } from '../../hooks/useMarkedDays';

interface Props {
  date: string; // yyyy-MM-dd
  displayDate: string; // e.g. "15.02.2026"
  open: boolean;
  onClose: () => void;
}

export function MarkAsBackfillModal({ date, displayDate, open, onClose }: Props) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const mutation = useAddExcludedDay();

  useEffect(() => {
    if (open) {
      setReason('');
      setError(null);
    }
  }, [open]);

  if (!open) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setError('Bitte gib einen Grund an.');
      return;
    }
    try {
      await mutation.mutateAsync({ date, reason: reason.trim() });
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unbekannter Fehler');
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-xl border border-stone-200 bg-white p-6 shadow-xl dark:border-stone-800 dark:bg-stone-900"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold">Als Massenimport markieren</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-stone-400 hover:bg-stone-100 hover:text-stone-600 dark:hover:bg-stone-800"
            aria-label="Schliessen"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <p className="mb-4 text-sm text-stone-600 dark:text-stone-400">
          Der Tag <strong>{displayDate}</strong> wird aus Tages-, Wochen- und
          Monats-Aggregaten ausgeblendet und als grauer Balken dargestellt.
        </p>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="reason" className="mb-1 block text-sm font-medium">
              Grund
            </label>
            <input
              id="reason"
              type="text"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="z.B. BSH API Ausfall, Initialimport ..."
              className="w-full rounded-md border border-stone-300 bg-white px-3 py-2 text-sm focus:border-coffee-500 focus:outline-none focus:ring-1 focus:ring-coffee-500 dark:border-stone-700 dark:bg-stone-800"
              autoFocus
              maxLength={500}
            />
          </div>

          {error && (
            <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
          )}

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-stone-300 px-3 py-1.5 text-sm font-medium hover:bg-stone-100 dark:border-stone-700 dark:hover:bg-stone-800"
              disabled={mutation.isPending}
            >
              Abbrechen
            </button>
            <button
              type="submit"
              disabled={mutation.isPending || !reason.trim()}
              className="rounded-md bg-coffee-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-coffee-700 disabled:opacity-50"
            >
              {mutation.isPending ? 'Speichere...' : 'Markieren'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
