import { fetchJson } from './client';
import type { CoffeeStatus } from './types';

export function fetchCoffeeStatus() {
  return fetchJson<CoffeeStatus>('/coffee/status');
}

export async function setCoffeePower(state: 'on' | 'off'): Promise<void> {
  await fetchJson('/coffee/power', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ state }),
  });
}
