import { useMemo } from 'react';
import type { DailyAggregate } from '../api/types';
import { ANOMALY_Z_SCORE_THRESHOLD, detectAnomalies } from '../lib/anomalyUtils';

export function useAnomalyDetection(
  data: DailyAggregate[] | undefined,
  excludedFromAnomaly: Set<string>,
  threshold = ANOMALY_Z_SCORE_THRESHOLD,
) {
  return useMemo(() => {
    if (!data) return [];
    const filtered = data.filter((d) => !excludedFromAnomaly.has(d.date));
    return detectAnomalies(filtered, threshold);
  }, [data, excludedFromAnomaly, threshold]);
}
