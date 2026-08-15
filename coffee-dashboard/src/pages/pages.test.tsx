import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { cloneElement, type ReactElement } from 'react';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DashboardPage } from './DashboardPage';
import { HeatmapPage } from './HeatmapPage';
import { LogPage } from './LogPage';
import { renderWithQuery } from '../test/renderWithQuery';
import * as statsApi from '../api/stats';
import type {
  DailyStatsResponse,
  HeatmapResponse,
  MarkedDay,
  PaginatedResponse,
  RangeStatsResponse,
  SnapshotResponse,
} from '../api/types';

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
    operationState: 'Ready',
  };
}

const daily: DailyStatsResponse = {
  date: '2026-08-15',
  snapshots: [snapshot(1, '2026-08-15T06:10:00Z', 10), snapshot(2, '2026-08-15T07:10:00Z', 13)],
  summary: { coffeeToday: 3, milkDrinksToday: 1, totalToday: 4, peakHour: 7 },
};

const range: RangeStatsResponse = {
  from: '2026-08-10',
  to: '2026-08-16',
  data: [
    { date: '2026-08-10', coffeeCount: 4, milkCount: 1, total: 5 },
    { date: '2026-08-11', coffeeCount: 6, milkCount: 2, total: 8 },
    { date: '2026-08-12', coffeeCount: 5, milkCount: 1, total: 6 },
  ],
};

const heatmap: HeatmapResponse = {
  weeks: 4,
  heatmap: [
    { dayOfWeek: 1, hour: 7, count: 5 },
    { dayOfWeek: 3, hour: 9, count: 2 },
  ],
};

const page1: PaginatedResponse<SnapshotResponse> = {
  data: [snapshot(2, '2026-08-15T07:10:00Z', 13), snapshot(1, '2026-08-15T06:10:00Z', 10)],
  pagination: { page: 1, pageSize: 25, totalItems: 40, totalPages: 2 },
};

const massImport: MarkedDay = {
  date: '2026-08-15',
  kind: 'mass-import',
  eventType: null,
  reason: 'BSH Ausfall',
  createdAt: '2026-08-15T09:00:00Z',
};

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.spyOn(statsApi, 'fetchDaily').mockResolvedValue(daily);
    vi.spyOn(statsApi, 'fetchRange').mockResolvedValue(range);
    vi.spyOn(statsApi, 'fetchHeatmap').mockResolvedValue(heatmap);
    vi.spyOn(statsApi, 'fetchLatestSnapshot').mockResolvedValue(snapshot(2, '2026-08-15T07:10:00Z', 13));
    vi.spyOn(statsApi, 'fetchMarkedDays').mockResolvedValue([]);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders KPIs and charts once the queries resolve', async () => {
    renderWithQuery(<DashboardPage />);

    expect(await screen.findByText('Taeglicher Verbrauch')).toBeInTheDocument();
    expect(screen.getByText('Kaffee: 3 · Milch: 1')).toBeInTheDocument();
    expect(screen.getByText('Verbrauchs-Trend')).toBeInTheDocument();
    expect(screen.getByText('Wochentage')).toBeInTheDocument();
  });

  it('reloads the range when the period changes', async () => {
    renderWithQuery(<DashboardPage />);
    await screen.findByText('Taeglicher Verbrauch');

    await userEvent.click(screen.getByRole('button', { name: 'Jahr' }));

    await waitFor(() => expect(statsApi.fetchRange).toHaveBeenCalledTimes(2));
    expect(screen.getAllByText('Dieses Jahr').length).toBeGreaterThan(0);
  });

  it('shows the error state when the daily query fails', async () => {
    vi.mocked(statsApi.fetchDaily).mockRejectedValue(new Error('boom'));
    renderWithQuery(<DashboardPage />);

    expect(await screen.findByText('Daten konnten nicht geladen werden.')).toBeInTheDocument();
  });
});

describe('HeatmapPage', () => {
  beforeEach(() => {
    vi.spyOn(statsApi, 'fetchHeatmap').mockResolvedValue(heatmap);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders the grid and the legend', async () => {
    renderWithQuery(<HeatmapPage />);

    expect(await screen.findByText('Wenig')).toBeInTheDocument();
    expect(screen.getAllByText('Mo').length).toBeGreaterThan(0);
  });

  it('refetches with the selected week count', async () => {
    renderWithQuery(<HeatmapPage />);
    await screen.findByText('Wenig');

    await userEvent.click(screen.getByRole('button', { name: '12w' }));

    await waitFor(() => expect(statsApi.fetchHeatmap).toHaveBeenCalledWith(12));
  });

  it('shows the error state when the heatmap query fails', async () => {
    vi.mocked(statsApi.fetchHeatmap).mockRejectedValue(new Error('boom'));
    renderWithQuery(<HeatmapPage />);

    expect(await screen.findByText('Daten konnten nicht geladen werden.')).toBeInTheDocument();
  });
});

describe('LogPage', () => {
  beforeEach(() => {
    vi.spyOn(statsApi, 'fetchSnapshots').mockResolvedValue(page1);
    vi.spyOn(statsApi, 'fetchMarkedDays').mockResolvedValue([]);
    vi.spyOn(statsApi, 'removeMarkedDay').mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('lists snapshots with their per-row delta', async () => {
    renderWithQuery(<LogPage />);

    expect(await screen.findByText('Snapshot Log')).toBeInTheDocument();
    expect(screen.getByText('40 Snapshots')).toBeInTheDocument();
    expect(screen.getByText('+3')).toBeInTheDocument();
  });

  it('pages forward and back', async () => {
    renderWithQuery(<LogPage />);
    await screen.findByText('Snapshot Log');

    expect(screen.getByRole('button', { name: /Zurueck/ })).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: /Weiter/ }));

    await waitFor(() => expect(statsApi.fetchSnapshots).toHaveBeenCalledWith(2, 25));
  });

  it('opens the backfill modal for an unmarked day', async () => {
    renderWithQuery(<LogPage />);
    await screen.findByText('Snapshot Log');

    await userEvent.click(screen.getAllByRole('button', { name: /markieren/ })[0]);

    expect(await screen.findByText('Als Massenimport markieren')).toBeInTheDocument();
  });

  it('removes an existing mass-import marking', async () => {
    vi.mocked(statsApi.fetchMarkedDays).mockResolvedValue([massImport]);
    renderWithQuery(<LogPage />);
    await screen.findByText('Snapshot Log');

    await userEvent.click((await screen.findAllByRole('button', { name: /Massenimport/ }))[0]);

    await waitFor(() => expect(statsApi.removeMarkedDay).toHaveBeenCalledWith('2026-08-15'));
  });

  it('shows the error state when the snapshot query fails', async () => {
    vi.mocked(statsApi.fetchSnapshots).mockRejectedValue(new Error('boom'));
    renderWithQuery(<LogPage />);

    expect(await screen.findByText('Daten konnten nicht geladen werden.')).toBeInTheDocument();
  });
});
