import { useState } from 'react';
import { HeatmapGrid } from '../components/charts/HeatmapGrid';
import { LoadingSpinner } from '../components/shared/LoadingSpinner';
import { ErrorMessage } from '../components/shared/ErrorMessage';
import { useHeatmap } from '../hooks/useHeatmap';

const weekOptions = [4, 8, 12, 26, 52] as const;

export function HeatmapPage() {
  const [weeks, setWeeks] = useState<number>(4);
  const { data, isLoading, isError } = useHeatmap(weeks);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-bold">Verbrauchs-Heatmap</h1>

        <div className="inline-flex rounded-lg bg-stone-200 p-1 dark:bg-stone-800">
          {weekOptions.map((w) => (
            <button
              key={w}
              onClick={() => setWeeks(w)}
              className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                weeks === w
                  ? 'bg-white text-coffee-700 shadow-sm dark:bg-stone-700 dark:text-coffee-200'
                  : 'text-stone-600 hover:text-stone-900 dark:text-stone-400 dark:hover:text-stone-200'
              }`}
            >
              {w}w
            </button>
          ))}
        </div>
      </div>

      <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
        <p className="mb-4 text-sm text-stone-500 dark:text-stone-400">
          Stunde (X) x Wochentag (Y) — letzte {weeks} Wochen. Farbintensitaet = Anzahl Getraenke.
        </p>

        {isLoading ? (
          <LoadingSpinner />
        ) : isError ? (
          <ErrorMessage />
        ) : data ? (
          <>
            <HeatmapGrid data={data.heatmap} />
            <div className="mt-4 flex items-center gap-2 text-xs text-stone-500 dark:text-stone-400">
              <span>Wenig</span>
              <span className="inline-block h-4 w-4 rounded bg-[#f5f0e8]" />
              <span className="inline-block h-4 w-4 rounded bg-[#e8c98f]" />
              <span className="inline-block h-4 w-4 rounded bg-[#d4a55a]" />
              <span className="inline-block h-4 w-4 rounded bg-[#8b5e1a]" />
              <span className="inline-block h-4 w-4 rounded bg-[#4a320d]" />
              <span>Viel</span>
            </div>
          </>
        ) : null}
      </div>
    </div>
  );
}
