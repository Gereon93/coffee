import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  fetchDaily,
  fetchRange,
  fetchHeatmap,
  fetchHealth,
  fetchSnapshots,
  fetchLatestSnapshot,
  fetchMarkedDays,
  addMarkedDay,
  removeMarkedDay,
} from './stats';
import { fetchCoffeeStatus, setCoffeePower } from './coffee';
import type { SnapshotResponse } from './types';

function jsonResponse(body: unknown, init: ResponseInit = {}) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
    ...init,
  });
}

function requestedUrl(call = 0): string {
  return String(vi.mocked(fetch).mock.calls[call][0]);
}

const snapshot: SnapshotResponse = {
  id: 1,
  timestamp: '2026-08-15T07:15:00Z',
  totalBeverages: 12,
  beverageCounterCoffee: 10,
  beverageCounterCoffeeAndMilk: 1,
  beverageCounterMilk: 1,
  beverageCounterHotWaterCups: 0,
  beverageCounterHotWater: 0,
  operationState: 'ready',
};

describe('stats api', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse({ data: [], pagination: {} }))));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('appends the client timezone offset to daily, range and heatmap requests', async () => {
    await fetchDaily('2026-08-15');
    await fetchRange('2026-08-01', '2026-08-15');
    await fetchHeatmap(8);

    expect(requestedUrl(0)).toMatch(/^\/api\/stats\/daily\/2026-08-15\?tz=-?\d+$/);
    expect(requestedUrl(1)).toMatch(/^\/api\/stats\/range\?from=2026-08-01&to=2026-08-15&tz=-?\d+$/);
    expect(requestedUrl(2)).toMatch(/^\/api\/stats\/heatmap\?weeks=8&tz=-?\d+$/);
  });

  it('requests health without a timezone parameter', async () => {
    await fetchHealth();

    expect(requestedUrl()).toBe('/api/health');
  });

  it('paginates snapshot requests', async () => {
    await fetchSnapshots(3, 25);

    expect(requestedUrl()).toBe('/api/stats?page=3&pageSize=25');
  });

  it('returns the single newest snapshot, or null when there is none', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse({ data: [snapshot], pagination: {} }));
    await expect(fetchLatestSnapshot()).resolves.toEqual(snapshot);

    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse({ data: [], pagination: {} }));
    await expect(fetchLatestSnapshot()).resolves.toBeNull();
  });

  it('filters marked days by kind only when a kind is given', async () => {
    await fetchMarkedDays();
    await fetchMarkedDays('event');

    expect(requestedUrl(0)).toBe('/api/stats/marked-days');
    expect(requestedUrl(1)).toBe('/api/stats/marked-days?kind=event');
  });

  it('posts a marked day as JSON', async () => {
    await addMarkedDay({ date: '2026-08-15', kind: 'event', eventType: 'party', reason: 'Feier' });

    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(String(url)).toBe('/api/stats/marked-days');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(String(init?.body))).toMatchObject({ date: '2026-08-15', kind: 'event' });
  });

  it('surfaces the API error message when marking fails', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse({ error: 'Day already marked' }, { status: 409 }),
    );

    await expect(
      addMarkedDay({ date: '2026-08-15', kind: 'mass-import', reason: 'x' }),
    ).rejects.toThrow('Day already marked');
  });

  it('deletes a marked day and tolerates 204', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(removeMarkedDay('2026-08-15')).resolves.toBeUndefined();
    expect(vi.mocked(fetch).mock.calls[0][1]?.method).toBe('DELETE');
  });

  it('reports a failed delete', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse({ error: 'Not marked' }, { status: 404 }));

    await expect(removeMarkedDay('2026-08-15')).rejects.toThrow('Not marked');
  });
});

describe('coffee api', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse({ reachable: true }))));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the live status', async () => {
    await fetchCoffeeStatus();

    expect(requestedUrl()).toBe('/coffee/status');
  });

  it('posts the requested power state', async () => {
    await setCoffeePower('on');

    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(String(url)).toBe('/coffee/power');
    expect(JSON.parse(String(init?.body))).toEqual({ state: 'on' });
  });

  it('surfaces the API message when switching fails', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse({ message: 'Webhook nicht erreichbar' }, { status: 500 }),
    );

    await expect(setCoffeePower('off')).rejects.toThrow('Webhook nicht erreichbar');
  });
});
