import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fetchExcludedDays,
  addExcludedDay,
  removeExcludedDay,
} from '../api/stats';
import type { CreateExcludedDayPayload } from '../api/types';

const QUERY_KEY = ['excluded-days'] as const;

export function useExcludedDays() {
  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: fetchExcludedDays,
    staleTime: 60_000,
  });
}

export function useAddExcludedDay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateExcludedDayPayload) => addExcludedDay(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEY });
      qc.invalidateQueries({ queryKey: ['range'] });
      qc.invalidateQueries({ queryKey: ['daily'] });
    },
  });
}

export function useRemoveExcludedDay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (date: string) => removeExcludedDay(date),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEY });
      qc.invalidateQueries({ queryKey: ['range'] });
      qc.invalidateQueries({ queryKey: ['daily'] });
    },
  });
}
