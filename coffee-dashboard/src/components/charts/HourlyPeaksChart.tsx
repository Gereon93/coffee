import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  Cell,
} from 'recharts';
import type { SnapshotResponse } from '../../api/types';
import { formatHour } from '../../lib/formatters';

interface Props {
  snapshots: SnapshotResponse[];
}

interface HourlyBucket {
  hour: number;
  label: string;
  delta: number;
  isPeak: boolean;
}

function buildHourlyData(snapshots: SnapshotResponse[]): HourlyBucket[] {
  if (snapshots.length < 2) return [];

  const sorted = [...snapshots].sort(
    (a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime(),
  );

  // Accumulate deltas per hour
  const hourMap = new Map<number, number>();
  for (let i = 1; i < sorted.length; i++) {
    const delta = sorted[i].totalBeverages - sorted[i - 1].totalBeverages;
    if (delta > 0) {
      const hour = new Date(sorted[i].timestamp).getHours();
      hourMap.set(hour, (hourMap.get(hour) ?? 0) + delta);
    }
  }

  if (hourMap.size === 0) return [];

  const maxDelta = Math.max(...hourMap.values());
  const buckets: HourlyBucket[] = [];

  // Show range from 6:00 to 23:00
  for (let h = 6; h <= 23; h++) {
    const delta = hourMap.get(h) ?? 0;
    buckets.push({
      hour: h,
      label: formatHour(h),
      delta,
      isPeak: delta > 0 && delta === maxDelta,
    });
  }

  return buckets;
}

export function HourlyPeaksChart({ snapshots }: Props) {
  const data = buildHourlyData(snapshots);

  if (data.length === 0) {
    return (
      <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
        <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
          Heutige Peaks
        </h3>
        <p className="py-12 text-center text-sm text-stone-400">
          Noch nicht genug Daten heute
        </p>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
        Heutige Peaks
      </h3>
      <ResponsiveContainer width="100%" height={250}>
        <BarChart data={data}>
          <XAxis
            dataKey="label"
            tick={{ fontSize: 11 }}
            stroke="currentColor"
            className="text-stone-400"
            interval={1}
          />
          <YAxis
            tick={{ fontSize: 12 }}
            stroke="currentColor"
            className="text-stone-400"
            allowDecimals={false}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: 'var(--color-stone-900, #1c1917)',
              border: 'none',
              borderRadius: '0.5rem',
              color: '#e7e5e4',
              fontSize: '0.875rem',
            }}
            itemStyle={{ color: '#e7e5e4' }}
            labelStyle={{ color: '#a8a29e' }}
            formatter={(value?: number | string) => [value ?? 0, 'Bezuege']}
            labelFormatter={(label) => `${label} Uhr`}
          />
          <Bar dataKey="delta" radius={[4, 4, 0, 0]}>
            {data.map((entry, i) => (
              <Cell
                key={i}
                fill={entry.isPeak ? '#ea580c' : '#d97706'}
                stroke={entry.isPeak ? '#c2410c' : 'none'}
                strokeWidth={entry.isPeak ? 2 : 0}
              />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
      <div className="mt-3 flex items-center gap-4 text-xs text-stone-500 dark:text-stone-400">
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-amber-600" /> Bezuege/Stunde
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-orange-600" /> Peak
        </span>
      </div>
    </div>
  );
}
