import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import type { HeatmapDataPoint } from '../../api/types';
import { dayLabel } from '../../lib/formatters';

interface Props {
  heatmap: HeatmapDataPoint[];
}

interface WeekdayBucket {
  dayOfWeek: number;
  label: string;
  avg: number;
}

function buildWeekdayData(heatmap: HeatmapDataPoint[]): WeekdayBucket[] {
  // Sum counts per day-of-week across all hours
  const dayTotals = new Map<number, number>();
  for (const point of heatmap) {
    dayTotals.set(
      point.dayOfWeek,
      (dayTotals.get(point.dayOfWeek) ?? 0) + point.count,
    );
  }

  const buckets: WeekdayBucket[] = [];
  for (let d = 1; d <= 7; d++) {
    buckets.push({
      dayOfWeek: d,
      label: dayLabel(d),
      avg: dayTotals.get(d) ?? 0,
    });
  }

  return buckets;
}

export function WeekdayComparisonChart({ heatmap }: Props) {
  const data = buildWeekdayData(heatmap);
  const hasData = data.some((d) => d.avg > 0);

  if (!hasData) {
    return (
      <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
        <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
          Wochentage
        </h3>
        <p className="py-12 text-center text-sm text-stone-400">Keine Daten</p>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
        Wochentage
      </h3>
      <ResponsiveContainer width="100%" height={250}>
        <BarChart data={data}>
          <XAxis
            dataKey="label"
            tick={{ fontSize: 12 }}
            stroke="currentColor"
            className="text-stone-400"
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
          />
          <Bar
            dataKey="avg"
            fill="#d97706"
            radius={[4, 4, 0, 0]}
          />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
