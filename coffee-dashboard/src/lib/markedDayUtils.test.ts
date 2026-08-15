import { describe, it, expect } from 'vitest';
import { buildMarkedDayMaps } from './markedDayUtils';
import type { MarkedDay } from '../api/types';

const massImport: MarkedDay = {
  date: '2026-08-01',
  kind: 'mass-import',
  eventType: null,
  reason: 'BSH API Ausfall',
  createdAt: '2026-08-02T08:00:00Z',
};

const event: MarkedDay = {
  date: '2026-08-05',
  kind: 'event',
  eventType: 'birthday',
  reason: 'Geburtstag',
  createdAt: '2026-08-05T08:00:00Z',
};

describe('buildMarkedDayMaps', () => {
  it('returns empty maps for undefined input', () => {
    const maps = buildMarkedDayMaps(undefined);

    expect(maps.byDate.size).toBe(0);
    expect(maps.massImportDates.size).toBe(0);
    expect(maps.eventDates.size).toBe(0);
    expect(maps.allMarkedDates.size).toBe(0);
  });

  it('separates mass-import from event days', () => {
    const maps = buildMarkedDayMaps([massImport, event]);

    expect([...maps.massImportDates]).toEqual(['2026-08-01']);
    expect([...maps.eventDates]).toEqual(['2026-08-05']);
  });

  it('keeps both kinds in the anomaly-exclusion union', () => {
    const maps = buildMarkedDayMaps([massImport, event]);

    expect([...maps.allMarkedDates].sort()).toEqual(['2026-08-01', '2026-08-05']);
  });

  it('indexes every marked day by its date', () => {
    const maps = buildMarkedDayMaps([massImport, event]);

    expect(maps.byDate.get('2026-08-05')).toBe(event);
    expect(maps.byDate.get('2026-08-01')?.reason).toBe('BSH API Ausfall');
  });
});
