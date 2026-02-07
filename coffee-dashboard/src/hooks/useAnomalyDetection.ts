import { useMemo } from 'react';
import type { DailyAggregate } from '../api/types';
import { detectAnomalies } from '../lib/anomalyUtils';

export function useAnomalyDetection(data: DailyAggregate[] | undefined, threshold = 1.5) {
  return useMemo(
    () => (data ? detectAnomalies(data, threshold) : []),
    [data, threshold],
  );
}
