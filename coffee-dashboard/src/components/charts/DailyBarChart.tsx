import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell,
} from 'recharts';
import type { DailyAggregate } from '../../api/types';
import type { AnomalyResult } from '../../lib/anomalyUtils';
import { formatDate } from '../../lib/dateUtils';

interface Props {
  data: DailyAggregate[];
  anomalies: AnomalyResult[];
  excludedSet: Set<string>;
}

export function DailyBarChart({ data, anomalies, excludedSet }: Props) {
  const anomalyDates = new Set(
    anomalies.filter((a) => a.isAnomaly).map((a) => a.date),
  );

  const chartData = data.map((d) => ({
    ...d,
    label: formatDate(d.date),
    isAnomaly: anomalyDates.has(d.date),
    isExcluded: excludedSet.has(d.date),
  }));

  const getCoffeeFill = (entry: typeof chartData[number]) => {
    if (entry.isExcluded) return '#a8a29e'; // stone-400
    if (entry.isAnomaly) return '#ef4444';
    return '#d97706';
  };

  const getMilkFill = (entry: typeof chartData[number]) => {
    if (entry.isExcluded) return '#d6d3d1'; // stone-300
    if (entry.isAnomaly) return '#fca5a5';
    return '#3b82f6';
  };

  const getStroke = (entry: typeof chartData[number]) => {
    if (entry.isExcluded) return '#78716c'; // stone-500
    if (entry.isAnomaly) return '#dc2626';
    return 'none';
  };

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
        Taeglicher Verbrauch
      </h3>
      <ResponsiveContainer width="100%" height={300}>
        <BarChart data={chartData}>
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
            formatter={(value?: number | string, name?: string, item?: { payload?: typeof chartData[number] }) => {
              const label = name === 'coffeeCount' ? 'Kaffee' : 'Milch';
              if (item?.payload?.isExcluded) {
                return [`${value ?? 0} (Massenimport)`, label];
              }
              return [value ?? 0, label];
            }}
          />
          <Bar dataKey="coffeeCount" stackId="a" radius={[0, 0, 0, 0]}>
            {chartData.map((entry, i) => (
              <Cell
                key={i}
                fill={getCoffeeFill(entry)}
                stroke={getStroke(entry)}
                strokeWidth={entry.isAnomaly || entry.isExcluded ? 2 : 0}
              />
            ))}
          </Bar>
          <Bar dataKey="milkCount" stackId="a" radius={[4, 4, 0, 0]}>
            {chartData.map((entry, i) => (
              <Cell
                key={i}
                fill={getMilkFill(entry)}
                stroke={getStroke(entry)}
                strokeWidth={entry.isAnomaly || entry.isExcluded ? 2 : 0}
              />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
      <div className="mt-3 flex flex-wrap items-center gap-4 text-xs text-stone-500 dark:text-stone-400">
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-amber-600" /> Kaffee
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-blue-500" /> Milch
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-red-500" /> Anomalie
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-stone-400" /> Massenimport
        </span>
      </div>
    </div>
  );
}
