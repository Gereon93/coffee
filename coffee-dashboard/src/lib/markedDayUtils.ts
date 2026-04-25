import type { DailyAggregate, MarkedDay } from '../api/types';

export interface MarkedDayMaps {
  byDate: Map<string, MarkedDay>;
  massImportDates: Set<string>;
  eventDates: Set<string>;
  /** Union of mass-import + event — for anomaly-detection baseline filter. */
  allMarkedDates: Set<string>;
}

export function buildMarkedDayMaps(marked: MarkedDay[] | undefined): MarkedDayMaps {
  const byDate = new Map<string, MarkedDay>();
  const massImportDates = new Set<string>();
  const eventDates = new Set<string>();
  const allMarkedDates = new Set<string>();

  for (const m of marked ?? []) {
    byDate.set(m.date, m);
    allMarkedDates.add(m.date);
    if (m.kind === 'mass-import') massImportDates.add(m.date);
    else if (m.kind === 'event') eventDates.add(m.date);
  }

  return { byDate, massImportDates, eventDates, allMarkedDates };
}

// Backward-compat helpers used by LogPage; will be inlined in Task 19.
export function buildExcludedSet(marked: MarkedDay[] | undefined): Set<string> {
  return buildMarkedDayMaps(marked).massImportDates;
}

export function isExcluded(date: string, excludedSet: Set<string>): boolean {
  return excludedSet.has(date);
}

export function filterNonExcluded(
  days: DailyAggregate[] | undefined,
  excludedSet: Set<string>,
): DailyAggregate[] {
  return (days ?? []).filter((d) => !excludedSet.has(d.date));
}
