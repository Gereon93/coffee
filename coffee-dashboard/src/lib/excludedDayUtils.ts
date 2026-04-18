import type { DailyAggregate, ExcludedDay } from '../api/types';

export function buildExcludedSet(excluded: ExcludedDay[] | undefined): Set<string> {
  return new Set((excluded ?? []).map((d) => d.date));
}

export function isExcluded(
  date: string,
  excludedSet: Set<string>,
): boolean {
  return excludedSet.has(date);
}

export function filterNonExcluded(
  days: DailyAggregate[] | undefined,
  excludedSet: Set<string>,
): DailyAggregate[] {
  return (days ?? []).filter((d) => !excludedSet.has(d.date));
}
