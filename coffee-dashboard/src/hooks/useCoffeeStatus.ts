import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchCoffeeStatus, setCoffeePower } from '../api/coffee';

const QUERY_KEY = ['coffee', 'status'] as const;

/**
 * How long BSH needs before a power change is visible in the status endpoint.
 * Refetching sooner reports the old state.
 */
const POWER_SETTLE_DELAY_MS = 3000;

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
      window.setTimeout(() => {
        qc.invalidateQueries({ queryKey: QUERY_KEY });
      }, POWER_SETTLE_DELAY_MS);
    },
  });
}
