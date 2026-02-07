import { useState, useMemo } from 'react';
import { type TimePeriod, getRange } from '../lib/dateUtils';

export function useTimePeriod(initial: TimePeriod = 'week') {
  const [period, setPeriod] = useState<TimePeriod>(initial);
  const range = useMemo(() => getRange(period), [period]);
  return { period, setPeriod, ...range };
}
