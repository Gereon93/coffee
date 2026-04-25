import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell, Customized,
} from 'recharts';
import type { DailyAggregate, MarkedDay } from '../../api/types';
import type { AnomalyResult } from '../../lib/anomalyUtils';
import { formatDate } from '../../lib/dateUtils';
import { emojiForEventType } from '../../lib/eventTypeMeta';

interface Props {
  data: DailyAggregate[];
  anomalies: AnomalyResult[];
  excludedSet: Set<string>;
  eventByDate: Map<string, MarkedDay>;
  onBarClick?: (date: string) => void;
}

export function DailyBarChart({
  data, anomalies, excludedSet, eventByDate, onBarClick,
}: Props) {
  const anomalyDates = new Set(
    anomalies.filter((a) => a.isAnomaly).map((a) => a.date),
  );

  const chartData = data.map((d) => {
    const event = eventByDate.get(d.date);
    return {
      ...d,
      label: formatDate(d.date),
      isAnomaly: anomalyDates.has(d.date),
      isExcluded: excludedSet.has(d.date),
      event: event && event.kind === 'event' ? event : null,
    };
  });

  type Entry = typeof chartData[number];

  const getCoffeeFill = (entry: Entry) => {
    if (entry.isExcluded) return '#a8a29e';
    if (entry.isAnomaly) return '#ef4444';
    return '#d97706';
  };
  const getMilkFill = (entry: Entry) => {
    if (entry.isExcluded) return '#d6d3d1';
    if (entry.isAnomaly) return '#fca5a5';
    return '#3b82f6';
  };
  const getStroke = (entry: Entry) => {
    if (entry.isExcluded) return '#78716c';
    if (entry.isAnomaly) return '#dc2626';
    return 'none';
  };

  // Custom layer that renders an emoji on top of each bar that has an event annotation.
  // Uses Recharts internal CategoricalChartProps via the Customized component.
  const EventBadges = (props: unknown) => {
    const p = props as {
      formattedGraphicalItems?: Array<{
        props: { data: Entry[] };
        item: { props: { dataKey: string } };
      }>;
      xAxisMap?: Record<string, { scale: (v: string) => number; bandwidth?: () => number }>;
      yAxisMap?: Record<string, { scale: (v: number) => number }>;
    };

    const x = p.xAxisMap ? Object.values(p.xAxisMap)[0] : null;
    const y = p.yAxisMap ? Object.values(p.yAxisMap)[0] : null;
    if (!x || !y) return null;

    const bandwidth = typeof x.bandwidth === 'function' ? x.bandwidth() : 24;

    return (
      <g>
        {chartData.map((entry, i) => {
          if (!entry.event) return null;
          const cx = x.scale(entry.label) + bandwidth / 2;
          const cy = y.scale(entry.total) - 8;
          if (Number.isNaN(cx) || Number.isNaN(cy)) return null;
          return (
            <text
              key={i}
              x={cx}
              y={cy}
              textAnchor="middle"
              fontSize={14}
              style={{ pointerEvents: 'none' }}
            >
              {emojiForEventType(entry.event.eventType ?? 'other')}
            </text>
          );
        })}
      </g>
    );
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
            formatter={(value?: number | string, name?: string, item?: { payload?: Entry }) => {
              const label = name === 'coffeeCount' ? 'Kaffee' : 'Milch';
              if (item?.payload?.isExcluded) {
                return [`${value ?? 0} (Massenimport)`, label];
              }
              if (item?.payload?.event && name === 'milkCount') {
                const e = item.payload.event;
                const note = e.reason ? ` — ${e.reason}` : '';
                return [`${value ?? 0}  ${emojiForEventType(e.eventType ?? 'other')}${note}`, label];
              }
              return [value ?? 0, label];
            }}
          />
          <Bar
            dataKey="coffeeCount"
            stackId="a"
            radius={[0, 0, 0, 0]}
            cursor="pointer"
            onClick={(entry: unknown) => {
              const e = entry as Entry | undefined;
              if (onBarClick && e?.date) onBarClick(e.date);
            }}
          >
            {chartData.map((entry, i) => (
              <Cell
                key={i}
                fill={getCoffeeFill(entry)}
                stroke={getStroke(entry)}
                strokeWidth={entry.isAnomaly || entry.isExcluded ? 2 : 0}
              />
            ))}
          </Bar>
          <Bar
            dataKey="milkCount"
            stackId="a"
            radius={[4, 4, 0, 0]}
            cursor="pointer"
            onClick={(entry: unknown) => {
              const e = entry as Entry | undefined;
              if (onBarClick && e?.date) onBarClick(e.date);
            }}
          >
            {chartData.map((entry, i) => (
              <Cell
                key={i}
                fill={getMilkFill(entry)}
                stroke={getStroke(entry)}
                strokeWidth={entry.isAnomaly || entry.isExcluded ? 2 : 0}
              />
            ))}
          </Bar>
          <Customized component={EventBadges} />
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
        <span className="flex items-center gap-1">
          <span>🎂</span> Event (klickbar zum Markieren)
        </span>
      </div>
    </div>
  );
}
