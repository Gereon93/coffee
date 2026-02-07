import { useQuery } from '@tanstack/react-query';
import { fetchHeatmap } from '../api/stats';

export function useHeatmap(weeks: number) {
  return useQuery({
    queryKey: ['heatmap', weeks],
    queryFn: () => fetchHeatmap(weeks),
    staleTime: 5 * 60_000,
  });
}
