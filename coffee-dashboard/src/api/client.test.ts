import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { fetchJson, ApiError } from './client';

function jsonResponse(body: unknown, init: ResponseInit = {}) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
    ...init,
  });
}

describe('fetchJson', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns the parsed body of a successful response', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ status: 'ok' }));

    await expect(fetchJson<{ status: string }>('/api/health')).resolves.toEqual({ status: 'ok' });
  });

  it('sends an Accept header and keeps caller headers', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}));

    await fetchJson('/api/health', { headers: { 'X-Test': '1' } });

    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect(init?.headers).toMatchObject({ Accept: 'application/json', 'X-Test': '1' });
  });

  it('throws an ApiError carrying the HTTP status', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({ error: 'nope' }, { status: 404, statusText: 'Not Found' }),
    );

    await expect(fetchJson('/api/stats')).rejects.toBeInstanceOf(ApiError);
    await expect(fetchJson('/api/stats')).rejects.toMatchObject({ status: 404 });
  });

  it('prefers the server-provided error message over the status line', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({ error: 'Day already marked' }, { status: 409, statusText: 'Conflict' }),
    );

    await expect(fetchJson('/api/stats')).rejects.toThrow('Day already marked');
  });

  it('falls back to a message field when no error field is present', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({ message: 'Webhook nicht erreichbar' }, { status: 500 }),
    );

    await expect(fetchJson('/coffee/power')).rejects.toThrow('Webhook nicht erreichbar');
  });

  it('resolves to null when the response has no body', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 204 }));

    await expect(fetchJson('/api/stats/marked-days/2026-08-15')).resolves.toBeNull();
  });
});
