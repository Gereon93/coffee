import type { DailyAggregate } from '../api/types';

/**
 * Z-score above which a day counts as an anomaly. 1.5 sigma flags the clearly
 * unusual days without drowning the badge in normal week-to-week variation.
 */
export const ANOMALY_Z_SCORE_THRESHOLD = 1.5;

/** Below this many days mean and standard deviation carry no information. */
const MIN_DAYS_FOR_DETECTION = 3;

export interface AnomalyResult {
  date: string;
  total: number;
  zScore: number;
  isAnomaly: boolean;
}

export function detectAnomalies(
  data: DailyAggregate[],
  threshold = ANOMALY_Z_SCORE_THRESHOLD,
): AnomalyResult[] {
  if (data.length < MIN_DAYS_FOR_DETECTION) {
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
