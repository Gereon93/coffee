import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import type { DailyAggregate } from '../../api/types';
import { formatDate } from '../../lib/dateUtils';

interface Props {
  data: DailyAggregate[];
}

export function TrendLineChart({ data }: Props) {
  const chartData = data.map((d) => ({
    label: formatDate(d.date),
    total: d.total,
  }));

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
        Verbrauchs-Trend
      </h3>
      <ResponsiveContainer width="100%" height={250}>
        <AreaChart data={chartData}>
          <defs>
            <linearGradient id="trendGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor="#d97706" stopOpacity={0.3} />
              <stop offset="95%" stopColor="#d97706" stopOpacity={0} />
            </linearGradient>
          </defs>
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
            formatter={(value?: number | string) => [value ?? 0, 'Gesamt']}
          />
          <Area
            type="monotone"
            dataKey="total"
            stroke="#d97706"
            strokeWidth={2}
            fill="url(#trendGrad)"
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}
