import type { Section, Setting } from './api.model';

const BASE = `${import.meta.env.BASE_URL}api`;

async function post<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body ?? {}),
  });

  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);

  const text = await res.text();
  return (text ? JSON.parse(text) : null) as T;
}

export const fetchConfiguration = (): Promise<Section[]> =>
  post<Section[]>('/GetConfiguration');

export const saveSettings = (
  settings: Setting[]
): Promise<string[]> =>
  post('/UpdateConfiguration', { settings });
