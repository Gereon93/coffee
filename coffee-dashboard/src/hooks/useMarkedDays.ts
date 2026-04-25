import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fetchMarkedDays,
  addMarkedDay,
  removeMarkedDay,
} from '../api/stats';
import type { CreateMarkedDayPayload, MarkedDayKind } from '../api/types';

const QUERY_KEY = ['marked-days'] as const;

export function useMarkedDays(kind?: MarkedDayKind) {
  return useQuery({
    queryKey: kind ? ['marked-days', kind] : QUERY_KEY,
    queryFn: () => fetchMarkedDays(kind),
    staleTime: 60_000,
  });
}

export function useAddMarkedDay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateMarkedDayPayload) => addMarkedDay(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['marked-days'] });
      qc.invalidateQueries({ queryKey: ['range'] });
      qc.invalidateQueries({ queryKey: ['daily'] });
    },
  });
}

export function useRemoveMarkedDay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (date: string) => removeMarkedDay(date),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['marked-days'] });
      qc.invalidateQueries({ queryKey: ['range'] });
      qc.invalidateQueries({ queryKey: ['daily'] });
    },
  });
}
