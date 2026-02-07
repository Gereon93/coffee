import type { HeatmapDataPoint } from '../../api/types';
import { dayLabel, formatHour } from '../../lib/formatters';

interface Props {
  data: HeatmapDataPoint[];
}

const HOURS = Array.from({ length: 18 }, (_, i) => i + 6); // 6-23
const DAYS = [1, 2, 3, 4, 5, 6, 7]; // Mo-So

const CELL_SIZE = 36;
const GAP = 3;
const LABEL_W = 32;
const LABEL_H = 24;

function getColor(count: number, max: number): string {
  if (count === 0 || max === 0) return '#f5f0e8'; // creme
  const ratio = count / max;
  if (ratio < 0.25) return '#e8c98f'; // light amber
  if (ratio < 0.5) return '#d4a55a'; // amber
  if (ratio < 0.75) return '#8b5e1a'; // coffee
  return '#4a320d'; // dark espresso
}

function getTextColor(count: number, max: number): string {
  if (max === 0) return '#78716c';
  const ratio = count / max;
  return ratio >= 0.5 ? '#fdf8f0' : '#44403c';
}

export function HeatmapGrid({ data }: Props) {
  const lookup = new Map<string, number>();
  let max = 0;
  for (const dp of data) {
    const key = `${dp.dayOfWeek}-${dp.hour}`;
    lookup.set(key, dp.count);
    if (dp.count > max) max = dp.count;
  }

  const svgW = LABEL_W + HOURS.length * (CELL_SIZE + GAP);
  const svgH = LABEL_H + DAYS.length * (CELL_SIZE + GAP);

  return (
    <div className="overflow-x-auto">
      <svg width={svgW} height={svgH} className="block">
        {/* Hour labels */}
        {HOURS.map((h, col) => (
          <text
            key={`hl-${h}`}
            x={LABEL_W + col * (CELL_SIZE + GAP) + CELL_SIZE / 2}
            y={16}
            textAnchor="middle"
            className="fill-stone-500 text-[11px] dark:fill-stone-400"
          >
            {formatHour(h)}
          </text>
        ))}

        {/* Day labels + cells */}
        {DAYS.map((day, row) => (
          <g key={`row-${day}`}>
            <text
              x={LABEL_W - 6}
              y={LABEL_H + row * (CELL_SIZE + GAP) + CELL_SIZE / 2 + 4}
              textAnchor="end"
              className="fill-stone-500 text-[11px] dark:fill-stone-400"
            >
              {dayLabel(day)}
            </text>
            {HOURS.map((hour, col) => {
              const count = lookup.get(`${day}-${hour}`) ?? 0;
              const x = LABEL_W + col * (CELL_SIZE + GAP);
              const y = LABEL_H + row * (CELL_SIZE + GAP);
              return (
                <g key={`c-${day}-${hour}`}>
                  <rect
                    x={x}
                    y={y}
                    width={CELL_SIZE}
                    height={CELL_SIZE}
                    rx={4}
                    fill={getColor(count, max)}
                  />
                  {count > 0 && (
                    <text
                      x={x + CELL_SIZE / 2}
                      y={y + CELL_SIZE / 2 + 4}
                      textAnchor="middle"
                      fill={getTextColor(count, max)}
                      className="text-[11px] font-medium"
                    >
                      {count}
                    </text>
                  )}
                </g>
              );
            })}
          </g>
        ))}
      </svg>
    </div>
  );
}
