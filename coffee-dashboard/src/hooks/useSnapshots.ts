import { useQuery } from '@tanstack/react-query';
import { fetchSnapshots } from '../api/stats';

export function useSnapshots(page: number, pageSize = 50) {
  return useQuery({
    queryKey: ['snapshots', page, pageSize],
    queryFn: () => fetchSnapshots(page, pageSize),
    staleTime: 30_000,
  });
}
