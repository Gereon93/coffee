import { fetchJson } from './client';
import type {
  DailyStatsResponse,
  RangeStatsResponse,
  HeatmapResponse,
  HealthResponse,
  PaginatedResponse,
  SnapshotResponse,
  MarkedDay,
  MarkedDayKind,
  CreateMarkedDayPayload,
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

export function fetchMarkedDays(kind?: MarkedDayKind) {
  const qs = kind ? `?kind=${kind}` : '';
  return fetchJson<MarkedDay[]>(`/api/stats/marked-days${qs}`);
}

export async function addMarkedDay(payload: CreateMarkedDayPayload): Promise<MarkedDay> {
  const res = await fetch('/api/stats/marked-days', {
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

export async function removeMarkedDay(date: string): Promise<void> {
  const res = await fetch(`/api/stats/marked-days/${date}`, { method: 'DELETE' });
  if (!res.ok && res.status !== 204) {
    const body = await res.json().catch(() => ({ error: 'Unknown error' }));
    throw new Error(body.error ?? `HTTP ${res.status}`);
  }
}
