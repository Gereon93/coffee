import { useState, useEffect } from 'react';
import { X, Trash2 } from 'lucide-react';
import { useAddMarkedDay, useRemoveMarkedDay } from '../../hooks/useMarkedDays';
import type { EventType, MarkedDay } from '../../api/types';
import { EVENT_TYPE_META } from '../../lib/eventTypeMeta';

interface Props {
  date: string;          // yyyy-MM-dd
  displayDate: string;   // "Mi 22.04.2026"
  existing: MarkedDay | null;
  open: boolean;
  onClose: () => void;
}

export function MarkDayEventModal({ date, displayDate, existing, open, onClose }: Props) {
  const isMassImport = existing?.kind === 'mass-import';
  const existingEvent = existing?.kind === 'event' ? existing : null;

  const [selected, setSelected] = useState<EventType | null>(existingEvent?.eventType ?? null);
  const [note, setNote] = useState(existingEvent?.reason ?? '');
  const [error, setError] = useState<string | null>(null);

  const addMutation = useAddMarkedDay();
  const removeMutation = useRemoveMarkedDay();

  useEffect(() => {
    if (open) {
      setSelected(existingEvent?.eventType ?? null);
      setNote(existingEvent?.reason ?? '');
      setError(null);
    }
  }, [open, existingEvent?.eventType, existingEvent?.reason]);

  if (!open) return null;

  const isPending = addMutation.isPending || removeMutation.isPending;

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selected) {
      setError('Bitte einen Anlass wählen.');
      return;
    }
    try {
      // If existing event annotation: delete first, then add (no PUT endpoint).
      if (existingEvent) {
        await removeMutation.mutateAsync(date);
      }
      await addMutation.mutateAsync({
        date,
        kind: 'event',
        eventType: selected,
        reason: note.trim(),
      });
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unbekannter Fehler');
    }
  };

  const handleRemove = async () => {
    setError(null);
    try {
      await removeMutation.mutateAsync(date);
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
          <h2 className="text-lg font-semibold">Tag markieren — {displayDate}</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-stone-400 hover:bg-stone-100 hover:text-stone-600 dark:hover:bg-stone-800"
            aria-label="Schliessen"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {isMassImport ? (
          <div className="space-y-3">
            <p className="text-sm text-stone-600 dark:text-stone-400">
              Dieser Tag ist als <strong>Massenimport</strong> markiert
              {existing?.reason ? ` (Grund: ${existing.reason})` : ''}.
              Massenimport-Markierungen können nur über die Log-Seite entfernt werden.
            </p>
            <div className="flex justify-end">
              <button
                type="button"
                onClick={onClose}
                className="rounded-md border border-stone-300 px-3 py-1.5 text-sm font-medium hover:bg-stone-100 dark:border-stone-700 dark:hover:bg-stone-800"
              >
                Schliessen
              </button>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSave} className="space-y-4">
            <p className="text-sm text-stone-600 dark:text-stone-400">
              Erkläre den Verbrauch an diesem Tag. Markierte Event-Tage werden nicht als Anomalie
              gewertet, bleiben aber in den Statistiken.
            </p>

            <div className="grid grid-cols-3 gap-2">
              {EVENT_TYPE_META.map((pick) => {
                const active = selected === pick.type;
                return (
                  <button
                    key={pick.type}
                    type="button"
                    onClick={() => setSelected(pick.type)}
                    className={`flex flex-col items-center gap-1 rounded-md border px-2 py-2 text-xs font-medium transition-colors ${
                      active
                        ? 'border-coffee-500 bg-coffee-50 text-coffee-700 dark:border-coffee-400 dark:bg-coffee-950 dark:text-coffee-200'
                        : 'border-stone-200 hover:bg-stone-50 dark:border-stone-700 dark:hover:bg-stone-800'
                    }`}
                  >
                    <span className="text-xl">{pick.emoji}</span>
                    {pick.label}
                  </button>
                );
              })}
            </div>

            <div>
              <label htmlFor="note" className="mb-1 block text-sm font-medium">
                Notiz (optional)
              </label>
              <input
                id="note"
                type="text"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="z.B. Schwiegereltern da"
                className="w-full rounded-md border border-stone-300 bg-white px-3 py-2 text-sm focus:border-coffee-500 focus:outline-none focus:ring-1 focus:ring-coffee-500 dark:border-stone-700 dark:bg-stone-800"
                maxLength={500}
              />
            </div>

            {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}

            <div className="flex items-center justify-between gap-2">
              <div>
                {existingEvent && (
                  <button
                    type="button"
                    onClick={handleRemove}
                    disabled={isPending}
                    className="inline-flex items-center gap-1 rounded-md border border-red-300 px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
                  >
                    <Trash2 className="h-3.5 w-3.5" /> Entfernen
                  </button>
                )}
              </div>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={onClose}
                  className="rounded-md border border-stone-300 px-3 py-1.5 text-sm font-medium hover:bg-stone-100 dark:border-stone-700 dark:hover:bg-stone-800"
                  disabled={isPending}
                >
                  Abbrechen
                </button>
                <button
                  type="submit"
                  disabled={isPending || !selected}
                  className="rounded-md bg-coffee-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-coffee-700 disabled:opacity-50"
                >
                  {isPending ? 'Speichere…' : existingEvent ? 'Aktualisieren' : 'Markieren'}
                </button>
              </div>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
