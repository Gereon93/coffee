import { Coffee, Milk, GlassWater } from 'lucide-react';
import { KpiCard } from './KpiCard';
import type { DailySummary, DailyAggregate, SnapshotResponse } from '../../api/types';
import type { TimePeriod } from '../../lib/dateUtils';

interface Props {
  summary: DailySummary | undefined;
  rangeData?: DailyAggregate[];
  excludedSet: Set<string>;
  period: TimePeriod;
  latestSnapshot?: SnapshotResponse | null;
}

const periodLabels: Record<TimePeriod, string> = {
  week: 'Diese Woche',
  month: 'Dieser Monat',
  year: 'Dieses Jahr',
  all: 'Gesamt',
};

export function KpiCardGrid({ summary, rangeData, excludedSet, period, latestSnapshot }: Props) {
  const s = summary ?? { totalToday: 0, coffeeToday: 0, milkDrinksToday: 0, peakHour: null };
  const label = periodLabels[period];

  const isAll = period === 'all' && latestSnapshot;
  const filteredRange = (rangeData ?? []).filter((d) => !excludedSet.has(d.date));

  const rangeCoffee = isAll
    ? latestSnapshot.beverageCounterCoffee
    : filteredRange.reduce((sum, d) => sum + d.coffeeCount, 0);

  const rangeMilk = isAll
    ? latestSnapshot.beverageCounterCoffeeAndMilk + latestSnapshot.beverageCounterMilk
    : filteredRange.reduce((sum, d) => sum + d.milkCount, 0);

  const rangeTotal = isAll
    ? latestSnapshot.totalBeverages
    : filteredRange.reduce((sum, d) => sum + d.total, 0);

  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      <KpiCard
        title="Heute"
        value={s.totalToday}
        icon={GlassWater}
        subtitle={`Kaffee: ${s.coffeeToday} · Milch: ${s.milkDrinksToday}`}
      />
      <KpiCard
        title={label}
        value={rangeTotal}
        icon={Coffee}
        subtitle="Alle Bezuege"
      />
      <KpiCard
        title="Kaffee"
        value={rangeCoffee}
        icon={Coffee}
        subtitle={label}
      />
      <KpiCard
        title="Milch"
        value={rangeMilk}
        icon={Milk}
        subtitle={label}
      />
    </div>
  );
}
