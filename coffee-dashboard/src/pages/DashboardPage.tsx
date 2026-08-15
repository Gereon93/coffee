import { useState } from 'react';
import { KpiCardGrid } from '../components/cards/KpiCardGrid';
import { TimePeriodSelector } from '../components/controls/TimePeriodSelector';
import { DailyBarChart } from '../components/charts/DailyBarChart';
import { TrendLineChart } from '../components/charts/TrendLineChart';
import { ConsumptionPieChart } from '../components/charts/ConsumptionPieChart';
import { HourlyPeaksChart } from '../components/charts/HourlyPeaksChart';
import { WeekdayComparisonChart } from '../components/charts/WeekdayComparisonChart';
import { QueryBoundary } from '../components/shared/QueryBoundary';
import { MarkDayEventModal } from '../components/dashboard/MarkDayEventModal';
import { useDailyStats } from '../hooks/useDailyStats';
import { useStatsRange } from '../hooks/useStatsRange';
import { useHeatmap } from '../hooks/useHeatmap';
import { useLatestSnapshot } from '../hooks/useLatestSnapshot';
import { useTimePeriod } from '../hooks/useTimePeriod';
import { useAnomalyDetection } from '../hooks/useAnomalyDetection';
import { useMarkedDays } from '../hooks/useMarkedDays';
import { buildMarkedDayMaps } from '../lib/markedDayUtils';
import { formatDisplayDate } from '../lib/dateUtils';

export function DashboardPage() {
  const { period, setPeriod, from, to } = useTimePeriod();
  const daily = useDailyStats();
  const range = useStatsRange(from, to);
  const heatmap = useHeatmap(4);
  const latest = useLatestSnapshot();
  const { data: marked } = useMarkedDays();
  const { byDate, massImportDates, allMarkedDates } = buildMarkedDayMaps(marked);
  const [modalDate, setModalDate] = useState<string | null>(null);
  const anomalies = useAnomalyDetection(range.data?.data, allMarkedDates);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-bold">Dashboard</h1>
        <TimePeriodSelector value={period} onChange={setPeriod} />
      </div>

      <QueryBoundary isLoading={daily.isLoading} isError={daily.isError}>
        <>
          <KpiCardGrid
            summary={daily.data?.summary}
            rangeData={range.data?.data}
            excludedSet={massImportDates}
            period={period}
            latestSnapshot={latest.data}
          />
          {daily.data?.snapshots && (
            <HourlyPeaksChart snapshots={daily.data.snapshots} />
          )}
        </>
      </QueryBoundary>

      <QueryBoundary isLoading={range.isLoading} isError={range.isError}>
        {range.data && (
          <>
            <DailyBarChart
              data={range.data.data}
              anomalies={anomalies}
              excludedSet={massImportDates}
              eventByDate={byDate}
              onBarClick={setModalDate}
            />

            <div className="grid gap-6 lg:grid-cols-2">
              <TrendLineChart data={range.data.data} />
              <ConsumptionPieChart data={range.data.data} />
            </div>

            <div className="grid gap-6 lg:grid-cols-2">
              {heatmap.data?.heatmap && (
                <WeekdayComparisonChart heatmap={heatmap.data.heatmap} />
              )}
            </div>
          </>
        )}
      </QueryBoundary>

      {modalDate && (
        <MarkDayEventModal
          key={modalDate}
          date={modalDate}
          displayDate={formatDisplayDate(modalDate)}
          existing={byDate.get(modalDate) ?? null}
          open={true}
          onClose={() => setModalDate(null)}
        />
      )}
    </div>
  );
}
