import { useState } from 'react';
import { ChevronLeft, ChevronRight, AlertCircle, Undo2 } from 'lucide-react';
import { useSnapshots } from '../hooks/useSnapshots';
import { useExcludedDays, useRemoveExcludedDay } from '../hooks/useExcludedDays';
import { LoadingSpinner } from '../components/shared/LoadingSpinner';
import { ErrorMessage } from '../components/shared/ErrorMessage';
import { MarkAsBackfillModal } from '../components/log/MarkAsBackfillModal';
import { buildExcludedSet } from '../lib/excludedDayUtils';
import type { SnapshotResponse } from '../api/types';

function formatLocalTime(isoTimestamp: string): string {
  return new Date(isoTimestamp).toLocaleString('de-DE', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  });
}

function toLocalDateKey(isoTimestamp: string): string {
  const d = new Date(isoTimestamp);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function formatDisplayDate(dateKey: string): string {
  const [y, m, d] = dateKey.split('-');
  return `${d}.${m}.${y}`;
}

function DeltaBadge({
  current, previous, field,
}: {
  current: SnapshotResponse;
  previous: SnapshotResponse | null;
  field: keyof Pick<SnapshotResponse, 'beverageCounterCoffee' | 'beverageCounterCoffeeAndMilk' | 'beverageCounterMilk' | 'beverageCounterHotWaterCups'>;
}) {
  if (!previous) return null;
  const delta = current[field] - previous[field];
  if (delta <= 0) return null;
  return (
    <span className="ml-1 inline-block rounded bg-amber-100 px-1 text-xs font-semibold text-amber-800 dark:bg-amber-900 dark:text-amber-200">
      +{delta}
    </span>
  );
}

export function LogPage() {
  const [page, setPage] = useState(1);
  const [modalDateKey, setModalDateKey] = useState<string | null>(null);
  const pageSize = 25;

  const { data, isLoading, isError } = useSnapshots(page, pageSize);
  const excluded = useExcludedDays();
  const removeMutation = useRemoveExcludedDay();

  if (isLoading) return <LoadingSpinner />;
  if (isError) return <ErrorMessage />;
  if (!data) return null;

  const { data: snapshots, pagination } = data;
  const excludedSet = buildExcludedSet(excluded.data);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Snapshot Log</h1>
        <span className="text-sm text-stone-500 dark:text-stone-400">
          {pagination.totalItems} Snapshots
        </span>
      </div>

      <div className="overflow-x-auto rounded-xl border border-stone-200 bg-white shadow-sm dark:border-stone-800 dark:bg-stone-900">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-stone-200 bg-stone-50 dark:border-stone-800 dark:bg-stone-950">
              <th className="px-3 py-2.5 font-semibold">ID</th>
              <th className="px-3 py-2.5 font-semibold">Zeitpunkt (Lokal)</th>
              <th className="px-3 py-2.5 font-semibold text-right">Kaffee</th>
              <th className="px-3 py-2.5 font-semibold text-right">Kaffee+Milch</th>
              <th className="px-3 py-2.5 font-semibold text-right">Milch</th>
              <th className="px-3 py-2.5 font-semibold text-right">Heisswasser</th>
              <th className="px-3 py-2.5 font-semibold text-right">Total</th>
              <th className="px-3 py-2.5 font-semibold">Status</th>
              <th className="px-3 py-2.5 font-semibold">Tag</th>
            </tr>
          </thead>
          <tbody>
            {snapshots.map((s: SnapshotResponse, i: number) => {
              const prev = i < snapshots.length - 1 ? snapshots[i + 1] : null;
              const dateKey = toLocalDateKey(s.timestamp);
              const isDayExcluded = excludedSet.has(dateKey);

              return (
                <tr
                  key={s.id}
                  className={`border-b border-stone-100 transition-colors hover:bg-stone-50 dark:border-stone-800/50 dark:hover:bg-stone-800/30 ${
                    isDayExcluded ? 'bg-stone-100/50 dark:bg-stone-800/20' : ''
                  }`}
                >
                  <td className="px-3 py-2 font-mono text-xs text-stone-500">{s.id}</td>
                  <td className="px-3 py-2 whitespace-nowrap">{formatLocalTime(s.timestamp)}</td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {s.beverageCounterCoffee}
                    <DeltaBadge current={s} previous={prev} field="beverageCounterCoffee" />
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {s.beverageCounterCoffeeAndMilk}
                    <DeltaBadge current={s} previous={prev} field="beverageCounterCoffeeAndMilk" />
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {s.beverageCounterMilk}
                    <DeltaBadge current={s} previous={prev} field="beverageCounterMilk" />
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {s.beverageCounterHotWaterCups}
                    <DeltaBadge current={s} previous={prev} field="beverageCounterHotWaterCups" />
                  </td>
                  <td className="px-3 py-2 text-right font-semibold tabular-nums">{s.totalBeverages}</td>
                  <td className="px-3 py-2">
                    <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${
                      s.operationState === 'Ready'
                        ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900 dark:text-emerald-300'
                        : 'bg-stone-100 text-stone-600 dark:bg-stone-800 dark:text-stone-400'
                    }`}>
                      {s.operationState}
                    </span>
                  </td>
                  <td className="px-3 py-2">
                    {isDayExcluded ? (
                      <button
                        type="button"
                        onClick={() => removeMutation.mutate(dateKey)}
                        disabled={removeMutation.isPending}
                        className="inline-flex items-center gap-1 rounded-full bg-stone-200 px-2 py-0.5 text-xs font-medium text-stone-700 hover:bg-stone-300 dark:bg-stone-700 dark:text-stone-300 dark:hover:bg-stone-600"
                        title="Markierung entfernen"
                      >
                        <Undo2 className="h-3 w-3" /> Massenimport
                      </button>
                    ) : (
                      <button
                        type="button"
                        onClick={() => setModalDateKey(dateKey)}
                        className="inline-flex items-center gap-1 rounded-full border border-stone-200 px-2 py-0.5 text-xs font-medium text-stone-500 hover:bg-stone-100 dark:border-stone-700 dark:text-stone-400 dark:hover:bg-stone-800"
                        title="Tag als Massenimport markieren"
                      >
                        <AlertCircle className="h-3 w-3" /> markieren
                      </button>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between">
        <span className="text-sm text-stone-500 dark:text-stone-400">
          Seite {pagination.page} von {pagination.totalPages}
        </span>
        <div className="flex gap-2">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1}
            className="flex items-center gap-1 rounded-lg border border-stone-200 px-3 py-1.5 text-sm font-medium transition-colors hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed dark:border-stone-700 dark:hover:bg-stone-800"
          >
            <ChevronLeft className="h-4 w-4" /> Zurueck
          </button>
          <button
            onClick={() => setPage((p) => Math.min(pagination.totalPages, p + 1))}
            disabled={page >= pagination.totalPages}
            className="flex items-center gap-1 rounded-lg border border-stone-200 px-3 py-1.5 text-sm font-medium transition-colors hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed dark:border-stone-700 dark:hover:bg-stone-800"
          >
            Weiter <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      </div>

      {modalDateKey && (
        <MarkAsBackfillModal
          date={modalDateKey}
          displayDate={formatDisplayDate(modalDateKey)}
          open={true}
          onClose={() => setModalDateKey(null)}
        />
      )}
    </div>
  );
}
