const BASE_URL = import.meta.env.VITE_API_BASE_URL || '';

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

export async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      'Accept': 'application/json',
      ...init?.headers,
    },
  });

  if (!res.ok) {
    throw new ApiError(res.status, `API ${res.status}: ${res.statusText}`);
  }

  return res.json();
}
