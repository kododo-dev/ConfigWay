import type { Section, Setting } from './api.model';

const BASE = `${import.meta.env.BASE_URL}api`;

/**
 * The API turned the request down (400, 404, 409) with a problem details response; `messages` are its
 * field errors, or its detail.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly messages: string[];

  constructor(status: number, messages: string[]) {
    super(messages.join(' '));
    this.status = status;
    this.messages = messages;
  }
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

async function toApiError(res: Response): Promise<Error> {
  if (!res.headers.get('Content-Type')?.includes('application/problem+json'))
    return new Error(`${res.status} ${res.statusText}`);
  const problem = (await res.json()) as ProblemDetails;
  const messages = problem.errors
    ? Object.values(problem.errors).flat()
    : [problem.detail ?? problem.title ?? `${res.status} ${res.statusText}`];
  return new ApiError(res.status, messages);
}

async function post<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body ?? {}),
  });

  if (!res.ok) throw await toApiError(res);

  const text = await res.text();
  return (text ? JSON.parse(text) : null) as T;
}

export const fetchConfiguration = (): Promise<Section[]> =>
  post<Section[]>('/GetConfiguration');

/** Throws {@link ApiError} with the validation messages when the settings are not valid. */
export const saveSettings = (
  settings: Setting[],
  keysToDelete: string[] = [],
): Promise<void> =>
  post<void>('/UpdateConfiguration', { settings, keysToDelete });
