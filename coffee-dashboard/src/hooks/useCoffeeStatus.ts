import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchCoffeeStatus, setCoffeePower } from '../api/coffee';

const QUERY_KEY = ['coffee', 'status'] as const;

export function useCoffeeStatus() {
  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: fetchCoffeeStatus,
    // On-demand semantics: no auto-refetch, no polling.
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    refetchOnMount: true,
    retry: 0,
  });
}

export function useSetCoffeePower() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (state: 'on' | 'off') => setCoffeePower(state),
    onSuccess: () => {
      // Wait ~3s for BSH to settle, then refresh status.
      window.setTimeout(() => {
        qc.invalidateQueries({ queryKey: QUERY_KEY });
      }, 3000);
    },
  });
}
