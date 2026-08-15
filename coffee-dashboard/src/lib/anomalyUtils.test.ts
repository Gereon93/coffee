import { describe, it, expect } from 'vitest';
import { detectAnomalies } from './anomalyUtils';
import type { DailyAggregate } from '../api/types';

function day(date: string, total: number): DailyAggregate {
  return { date, coffeeCount: total, milkCount: 0, total };
}

describe('detectAnomalies', () => {
  it('reports no anomaly for fewer than three days', () => {
    const result = detectAnomalies([day('2026-08-01', 3), day('2026-08-02', 99)]);

    expect(result.map((r) => r.isAnomaly)).toEqual([false, false]);
    expect(result.map((r) => r.zScore)).toEqual([0, 0]);
  });

  it('flags the day above the z-score threshold', () => {
    const result = detectAnomalies([
      day('2026-08-01', 4),
      day('2026-08-02', 4),
      day('2026-08-03', 4),
      day('2026-08-04', 20),
    ]);

    expect(result.filter((r) => r.isAnomaly).map((r) => r.date)).toEqual(['2026-08-04']);
  });

  it('reports no anomaly when every day is identical', () => {
    const result = detectAnomalies([
      day('2026-08-01', 5),
      day('2026-08-02', 5),
      day('2026-08-03', 5),
    ]);

    expect(result.every((r) => r.zScore === 0 && !r.isAnomaly)).toBe(true);
  });

  it('honours a custom threshold', () => {
    const data = [
      day('2026-08-01', 4),
      day('2026-08-02', 5),
      day('2026-08-03', 6),
      day('2026-08-04', 9),
    ];

    expect(detectAnomalies(data, 5).some((r) => r.isAnomaly)).toBe(false);
    expect(detectAnomalies(data, 1).some((r) => r.isAnomaly)).toBe(true);
  });

  it('never flags a day below the mean', () => {
    const result = detectAnomalies([
      day('2026-08-01', 1),
      day('2026-08-02', 10),
      day('2026-08-03', 10),
      day('2026-08-04', 10),
    ]);

    expect(result[0].zScore).toBeLessThan(0);
    expect(result[0].isAnomaly).toBe(false);
  });
});
