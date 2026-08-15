import { describe, it, expect, vi } from 'vitest';
import { cloneElement, type ReactElement } from 'react';
import { render, screen } from '@testing-library/react';
import { DailyBarChart } from './DailyBarChart';
import { TrendLineChart } from './TrendLineChart';
import { ConsumptionPieChart } from './ConsumptionPieChart';
import { HourlyPeaksChart } from './HourlyPeaksChart';
import { WeekdayComparisonChart } from './WeekdayComparisonChart';
import { HeatmapGrid } from './HeatmapGrid';
import type {
  DailyAggregate,
  HeatmapDataPoint,
  MarkedDay,
  SnapshotResponse,
} from '../../api/types';

vi.mock('recharts', async () => {
  const actual = await vi.importActual<typeof import('recharts')>('recharts');
  return {
    ...actual,
    ResponsiveContainer: ({ children }: { children: ReactElement }) =>
      cloneElement(children as ReactElement<{ width?: number; height?: number }>, {
        width: 600,
        height: 300,
      }),
  };
});

const range: DailyAggregate[] = [
  { date: '2026-08-01', coffeeCount: 4, milkCount: 1, total: 5 },
  { date: '2026-08-02', coffeeCount: 9, milkCount: 3, total: 12 },
  { date: '2026-08-03', coffeeCount: 2, milkCount: 0, total: 2 },
];

const birthday: MarkedDay = {
  date: '2026-08-02',
  kind: 'event',
  eventType: 'birthday',
  reason: 'Geburtstag',
  createdAt: '2026-08-02T06:00:00Z',
};

function snapshot(id: number, timestamp: string, coffee: number): SnapshotResponse {
  return {
    id,
    timestamp,
    totalBeverages: coffee,
    beverageCounterCoffee: coffee,
    beverageCounterCoffeeAndMilk: 0,
    beverageCounterMilk: 0,
    beverageCounterHotWaterCups: 0,
    beverageCounterHotWater: 0,
    operationState: 'ready',
  };
}

describe('DailyBarChart', () => {
  it('renders the chart heading and legend', () => {
    render(
      <DailyBarChart
        data={range}
        anomalies={[{ date: '2026-08-02', total: 12, zScore: 2, isAnomaly: true }]}
        excludedSet={new Set(['2026-08-03'])}
        eventByDate={new Map([[birthday.date, birthday]])}
      />,
    );

    expect(screen.getByText('Taeglicher Verbrauch')).toBeInTheDocument();
    expect(screen.getByText('Anomalie')).toBeInTheDocument();
    expect(screen.getByText('Massenimport')).toBeInTheDocument();
  });

  it('labels the x-axis with day and month', () => {
    render(
      <DailyBarChart
        data={range}
        anomalies={[]}
        excludedSet={new Set()}
        eventByDate={new Map()}
      />,
    );

    expect(screen.getByText('01.08.')).toBeInTheDocument();
  });
});

describe('TrendLineChart', () => {
  it('renders the trend heading', () => {
    render(<TrendLineChart data={range} />);

    expect(screen.getByText('Verbrauchs-Trend')).toBeInTheDocument();
  });
});

describe('ConsumptionPieChart', () => {
  it('renders the distribution for non-empty data', () => {
    render(<ConsumptionPieChart data={range} />);

    expect(screen.getByText('Verteilung')).toBeInTheDocument();
  });

  it('shows an empty state when nothing was consumed', () => {
    render(
      <ConsumptionPieChart
        data={[{ date: '2026-08-01', coffeeCount: 0, milkCount: 0, total: 0 }]}
      />,
    );

    expect(screen.getByText('Keine Daten')).toBeInTheDocument();
  });
});

describe('HourlyPeaksChart', () => {
  it('shows an empty state without snapshots', () => {
    render(<HourlyPeaksChart snapshots={[]} />);

    expect(screen.getByText('Noch nicht genug Daten heute')).toBeInTheDocument();
  });

  it('renders hourly buckets from consecutive snapshots', () => {
    render(
      <HourlyPeaksChart
        snapshots={[
          snapshot(1, '2026-08-15T06:10:00Z', 10),
          snapshot(2, '2026-08-15T07:10:00Z', 13),
          snapshot(3, '2026-08-15T08:10:00Z', 14),
        ]}
      />,
    );

    expect(screen.getByText('Heutige Peaks')).toBeInTheDocument();
  });
});

describe('WeekdayComparisonChart', () => {
  it('shows an empty state when every weekday is zero', () => {
    render(<WeekdayComparisonChart heatmap={[]} />);

    expect(screen.getByText('Keine Daten')).toBeInTheDocument();
  });

  it('renders the weekday chart when data exists', () => {
    const heatmap: HeatmapDataPoint[] = [
      { dayOfWeek: 1, hour: 7, count: 4 },
      { dayOfWeek: 2, hour: 8, count: 6 },
    ];

    render(<WeekdayComparisonChart heatmap={heatmap} />);

    expect(screen.getByText('Wochentage')).toBeInTheDocument();
  });
});

describe('HeatmapGrid', () => {
  it('renders weekday labels and hour labels', () => {
    const heatmap: HeatmapDataPoint[] = [
      { dayOfWeek: 1, hour: 7, count: 3 },
      { dayOfWeek: 7, hour: 9, count: 1 },
    ];

    render(<HeatmapGrid data={heatmap} />);

    expect(screen.getByText('Mo')).toBeInTheDocument();
    expect(screen.getByText('So')).toBeInTheDocument();
    expect(screen.getByText('07:00')).toBeInTheDocument();
  });
});
