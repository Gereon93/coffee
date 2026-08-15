import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Coffee } from 'lucide-react';
import { AnomalyBadge } from './anomaly/AnomalyBadge';
import { KpiCard } from './cards/KpiCard';
import { KpiCardGrid } from './cards/KpiCardGrid';
import { ErrorMessage } from './shared/ErrorMessage';
import { LoadingSpinner } from './shared/LoadingSpinner';
import { QueryBoundary } from './shared/QueryBoundary';
import { TimePeriodSelector } from './controls/TimePeriodSelector';
import type { DailyAggregate, DailySummary, SnapshotResponse } from '../api/types';

const summary: DailySummary = {
  coffeeToday: 3,
  milkDrinksToday: 2,
  totalToday: 5,
  peakHour: 8,
};

const rangeData: DailyAggregate[] = [
  { date: '2026-08-01', coffeeCount: 4, milkCount: 1, total: 5 },
  { date: '2026-08-02', coffeeCount: 6, milkCount: 2, total: 8 },
];

const latestSnapshot: SnapshotResponse = {
  id: 9,
  timestamp: '2026-08-15T07:15:00Z',
  totalBeverages: 1000,
  beverageCounterCoffee: 800,
  beverageCounterCoffeeAndMilk: 120,
  beverageCounterMilk: 80,
  beverageCounterHotWaterCups: 0,
  beverageCounterHotWater: 0,
  operationState: 'ready',
};

describe('AnomalyBadge', () => {
  it('renders the z-score with one decimal', () => {
    render(<AnomalyBadge zScore={2.345} />);

    expect(screen.getByText('2.3x')).toBeInTheDocument();
  });
});

describe('KpiCard', () => {
  it('renders title, value and subtitle', () => {
    render(<KpiCard title="Heute" value={5} icon={Coffee} subtitle="Kaffee: 3" />);

    expect(screen.getByText('Heute')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('Kaffee: 3')).toBeInTheDocument();
  });

  it('omits the subtitle when none is given', () => {
    render(<KpiCard title="Heute" value={5} icon={Coffee} />);

    expect(screen.queryByText('Kaffee: 3')).not.toBeInTheDocument();
  });
});

describe('KpiCardGrid', () => {
  it('sums the range and skips mass-import days', () => {
    render(
      <KpiCardGrid
        summary={summary}
        rangeData={rangeData}
        excludedSet={new Set(['2026-08-02'])}
        period="week"
      />,
    );

    expect(screen.getAllByText('Diese Woche').length).toBeGreaterThan(0);
    expect(screen.getByText('4')).toBeInTheDocument();
    expect(screen.getByText('Kaffee: 3 · Milch: 2')).toBeInTheDocument();
  });

  it('falls back to zeros without a summary', () => {
    render(<KpiCardGrid summary={undefined} excludedSet={new Set()} period="month" />);

    expect(screen.getByText('Kaffee: 0 · Milch: 0')).toBeInTheDocument();
    expect(screen.getAllByText('Dieser Monat').length).toBeGreaterThan(0);
  });

  it('shows absolute machine counters for the all period', () => {
    render(
      <KpiCardGrid
        summary={summary}
        rangeData={rangeData}
        excludedSet={new Set()}
        period="all"
        latestSnapshot={latestSnapshot}
      />,
    );

    expect(screen.getByText('1000')).toBeInTheDocument();
    expect(screen.getByText('800')).toBeInTheDocument();
    expect(screen.getByText('200')).toBeInTheDocument();
  });
});

describe('ErrorMessage', () => {
  it('shows the default message', () => {
    render(<ErrorMessage />);

    expect(screen.getByText('Daten konnten nicht geladen werden.')).toBeInTheDocument();
  });

  it('shows a custom message', () => {
    render(<ErrorMessage message="Kaputt" />);

    expect(screen.getByText('Kaputt')).toBeInTheDocument();
  });
});

describe('QueryBoundary', () => {
  it('renders the spinner while loading', () => {
    const { container } = render(
      <QueryBoundary isLoading isError={false}>
        <p>Inhalt</p>
      </QueryBoundary>,
    );

    expect(screen.queryByText('Inhalt')).not.toBeInTheDocument();
    expect(container.querySelector('svg')).toBeInTheDocument();
  });

  it('renders the error message on failure', () => {
    render(
      <QueryBoundary isLoading={false} isError>
        <p>Inhalt</p>
      </QueryBoundary>,
    );

    expect(screen.getByText('Daten konnten nicht geladen werden.')).toBeInTheDocument();
  });

  it('renders children once data is there', () => {
    render(
      <QueryBoundary isLoading={false} isError={false}>
        <p>Inhalt</p>
      </QueryBoundary>,
    );

    expect(screen.getByText('Inhalt')).toBeInTheDocument();
  });
});

describe('LoadingSpinner', () => {
  it('renders an icon', () => {
    const { container } = render(<LoadingSpinner />);

    expect(container.querySelector('svg')).toBeInTheDocument();
  });
});

describe('TimePeriodSelector', () => {
  it('reports the clicked period', async () => {
    const onChange = vi.fn();
    render(<TimePeriodSelector value="week" onChange={onChange} />);

    await userEvent.click(screen.getByRole('button', { name: 'Monat' }));

    expect(onChange).toHaveBeenCalledWith('month');
  });

  it('marks the active period', () => {
    render(<TimePeriodSelector value="year" onChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Jahr' }).className).toContain('shadow-sm');
    expect(screen.getByRole('button', { name: 'Woche' }).className).not.toContain('shadow-sm');
  });
});
