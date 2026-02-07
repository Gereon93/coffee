import { useQuery } from '@tanstack/react-query';
import { fetchRange } from '../api/stats';

export function useStatsRange(from: string, to: string) {
  return useQuery({
    queryKey: ['range', from, to],
    queryFn: () => fetchRange(from, to),
    staleTime: 60_000,
  });
}
