import { fetchJson } from './client';
import type {
  DailyStatsResponse,
  RangeStatsResponse,
  HeatmapResponse,
  HealthResponse,
  PaginatedResponse,
  SnapshotResponse,
  ExcludedDay,
  CreateExcludedDayPayload,
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

export function fetchExcludedDays() {
  return fetchJson<ExcludedDay[]>('/api/stats/excluded-days');
}

export async function addExcludedDay(payload: CreateExcludedDayPayload): Promise<ExcludedDay> {
  const res = await fetch('/api/stats/excluded-days', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({ error: 'Unknown error' }));
    throw new Error(body.error ?? `HTTP ${res.status}`);
  }
  return res.json();
}

export async function removeExcludedDay(date: string): Promise<void> {
  const res = await fetch(`/api/stats/excluded-days/${date}`, { method: 'DELETE' });
  if (!res.ok && res.status !== 204) {
    const body = await res.json().catch(() => ({ error: 'Unknown error' }));
    throw new Error(body.error ?? `HTTP ${res.status}`);
  }
}
