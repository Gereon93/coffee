import { fetchJson } from './client';
import type {
  DailyStatsResponse,
  RangeStatsResponse,
  HeatmapResponse,
  HealthResponse,
  PaginatedResponse,
  SnapshotResponse,
} from './types';

export function fetchDaily(date: string) {
  return fetchJson<DailyStatsResponse>(`/api/stats/daily/${date}`);
}

export function fetchRange(from: string, to: string) {
  return fetchJson<RangeStatsResponse>(
    `/api/stats/range?from=${from}&to=${to}`,
  );
}

export function fetchHeatmap(weeks: number) {
  return fetchJson<HeatmapResponse>(`/api/stats/heatmap?weeks=${weeks}`);
}

export function fetchHealth() {
  return fetchJson<HealthResponse>('/api/health');
}

export async function fetchLatestSnapshot() {
  const res = await fetchJson<PaginatedResponse<SnapshotResponse>>(
    '/api/stats?page=1&pageSize=1',
  );
  return res.data[0] ?? null;
}
