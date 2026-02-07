import { useQuery } from '@tanstack/react-query';
import { fetchDaily } from '../api/stats';
import { today } from '../lib/dateUtils';

export function useDailyStats(date?: string) {
  const d = date ?? today();
  return useQuery({
    queryKey: ['daily', d],
    queryFn: () => fetchDaily(d),
    staleTime: 60_000,
  });
}
