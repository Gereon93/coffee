import { fetchJson } from './client';
import type {
  DailyStatsResponse,
  RangeStatsResponse,
  HeatmapResponse,
  HealthResponse,
  PaginatedResponse,
  SnapshotResponse,
} from './types';

/** Browser UTC offset in minutes (positive for east of UTC, e.g. 60 for CET) */
const TZ = -new Date().getTimezoneOffset();

export function fetchDaily(date: string) {
  return fetchJson<DailyStatsResponse>(`/api/stats/daily/${date}?tz=${TZ}`);
}

export function fetchRange(from: string, to: string) {
  return fetchJson<RangeStatsResponse>(
    `/api/stats/range?from=${from}&to=${to}&tz=${TZ}`,
  );
}

export function fetchHeatmap(weeks: number) {
  return fetchJson<HeatmapResponse>(`/api/stats/heatmap?weeks=${weeks}&tz=${TZ}`);
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

export function fetchSnapshots(page: number, pageSize = 50) {
  return fetchJson<PaginatedResponse<SnapshotResponse>>(
    `/api/stats?page=${page}&pageSize=${pageSize}`,
  );
}
