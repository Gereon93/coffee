import type { DailyAggregate } from '../api/types';

export interface AnomalyResult {
  date: string;
  total: number;
  zScore: number;
  isAnomaly: boolean;
}

export function detectAnomalies(
  data: DailyAggregate[],
  threshold = 1.5,
): AnomalyResult[] {
  if (data.length < 3) {
    return data.map((d) => ({
      date: d.date,
      total: d.total,
      zScore: 0,
      isAnomaly: false,
    }));
  }

  const totals = data.map((d) => d.total);
  const mean = totals.reduce((a, b) => a + b, 0) / totals.length;
  const stdDev = Math.sqrt(
    totals.reduce((sum, v) => sum + (v - mean) ** 2, 0) / totals.length,
  );

  return data.map((d) => {
    const zScore = stdDev === 0 ? 0 : (d.total - mean) / stdDev;
    return {
      date: d.date,
      total: d.total,
      zScore,
      isAnomaly: zScore > threshold,
    };
  });
}
