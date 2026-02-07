import { useQuery } from '@tanstack/react-query';
import { fetchLatestSnapshot } from '../api/stats';

export function useLatestSnapshot() {
  return useQuery({
    queryKey: ['latestSnapshot'],
    queryFn: fetchLatestSnapshot,
    staleTime: 60_000,
  });
}
