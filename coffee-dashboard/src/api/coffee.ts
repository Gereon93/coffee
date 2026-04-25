import { fetchJson } from './client';
import type { CoffeeStatus } from './types';

export function fetchCoffeeStatus() {
  return fetchJson<CoffeeStatus>('/coffee/status');
}

export async function setCoffeePower(state: 'on' | 'off'): Promise<void> {
  const res = await fetch('/coffee/power', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ state }),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({ message: `HTTP ${res.status}` }));
    throw new Error(body.message ?? `HTTP ${res.status}`);
  }
}
