import type { FeatureFilter, FeatureFlag, FilterParameters } from '../api/flags';

/** The built-in filters the editor has forms for; others are edited as JSON. */
export type FilterKind = 'Percentage' | 'TimeWindow' | 'Targeting' | 'Custom';

export const FILTER_ALIASES: Record<Exclude<FilterKind, 'Custom'>, string> = {
  Percentage: 'Microsoft.Percentage',
  TimeWindow: 'Microsoft.TimeWindow',
  Targeting:  'Microsoft.Targeting',
};

/** The built-in filter a name refers to, by its full alias or its last segment, as feature management accepts them. */
export function filterKind(name: string): FilterKind {
  const lower = name.trim().toLowerCase();
  for (const [kind, alias] of Object.entries(FILTER_ALIASES)) {
    const a = alias.toLowerCase();
    if (lower === a || lower === a.slice(a.lastIndexOf('.') + 1)) return kind as FilterKind;
  }
  return 'Custom';
}

/** Whether the application registered a filter with this name (full alias or last segment). */
export function isRegisteredFilter(name: string, registered: string[]): boolean {
  const lower = name.trim().toLowerCase();
  return registered.some(r => {
    const a = r.toLowerCase();
    return lower === a || lower === a.slice(a.lastIndexOf('.') + 1);
  });
}

export function newFilter(kind: FilterKind): FeatureFilter {
  switch (kind) {
    case 'Percentage': return { name: FILTER_ALIASES.Percentage, parameters: { Value: 50 } };
    case 'TimeWindow': return { name: FILTER_ALIASES.TimeWindow, parameters: { Start: new Date().toISOString() } };
    case 'Targeting':  return {
      name: FILTER_ALIASES.Targeting,
      parameters: { Audience: { Users: [], Groups: [], DefaultRolloutPercentage: 0, Exclusion: { Users: [], Groups: [] } } },
    };
    case 'Custom':     return { name: '', parameters: {} };
  }
}

export function newFlag(): FeatureFlag {
  return { id: '', enabled: true, requirementType: 'Any', filters: [], description: null };
}

// ── Parameters ─────────────────────────────────────────────────────────────

export function asNumber(value: unknown): number | null {
  if (typeof value === 'number') return value;
  if (typeof value === 'string' && value.trim() !== '' && !isNaN(Number(value))) return Number(value);
  return null;
}

export function asText(value: unknown): string | null {
  return typeof value === 'string' ? value : typeof value === 'number' ? String(value) : null;
}

export function asTexts(value: unknown): string[] {
  return Array.isArray(value) ? value.map(v => asText(v) ?? '').filter(v => v !== '') : [];
}

export interface GroupRollout { Name: string; RolloutPercentage: number | null }

export interface Audience {
  Users: string[];
  Groups: GroupRollout[];
  DefaultRolloutPercentage: number | null;
  Exclusion: { Users: string[]; Groups: string[] };
}

export function readAudience(parameters: FilterParameters | null): Audience {
  const audience = (parameters?.Audience ?? {}) as Record<string, unknown>;
  const exclusion = (audience.Exclusion ?? {}) as Record<string, unknown>;
  return {
    Users: asTexts(audience.Users),
    Groups: Array.isArray(audience.Groups)
      ? audience.Groups.map(g => {
          const group = (g ?? {}) as Record<string, unknown>;
          return { Name: asText(group.Name) ?? '', RolloutPercentage: asNumber(group.RolloutPercentage) };
        })
      : [],
    DefaultRolloutPercentage: asNumber(audience.DefaultRolloutPercentage),
    Exclusion: { Users: asTexts(exclusion.Users), Groups: asTexts(exclusion.Groups) },
  };
}

// ── Time ───────────────────────────────────────────────────────────────────

/** A stored time (RFC 1123 or ISO 8601) as the value of a datetime-local input, in the browser's time zone. */
export function toLocalInput(text: string | null): string {
  if (!text) return '';
  const date = new Date(text);
  if (isNaN(date.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** The value of a datetime-local input (local time) as an ISO 8601 time in UTC; null when empty. */
export function fromLocalInput(value: string): string | null {
  if (!value) return null;
  const date = new Date(value);
  return isNaN(date.getTime()) ? null : date.toISOString();
}

export type WindowState = 'active' | 'upcoming' | 'ended' | 'unknown';

export function windowState(parameters: FilterParameters | null, now = new Date()): WindowState {
  const start = asText(parameters?.Start);
  const end = asText(parameters?.End);
  const startDate = start ? new Date(start) : null;
  const endDate = end ? new Date(end) : null;
  if ((startDate && isNaN(startDate.getTime())) || (endDate && isNaN(endDate.getTime()))) return 'unknown';
  if (!startDate && !endDate) return 'unknown';
  if (startDate && now < startDate) return 'upcoming';
  if (endDate && now >= endDate) return 'ended';
  return 'active';
}

export function formatTime(text: string | null, lang: string): string {
  if (!text) return '';
  const date = new Date(text);
  return isNaN(date.getTime())
    ? text
    : date.toLocaleString(lang, { dateStyle: 'medium', timeStyle: 'short' });
}

// ── Comparison and errors ──────────────────────────────────────────────────

/** JSON with sorted keys, so that two flags with the same values compare equal. */
export function stableJson(value: unknown): string {
  return JSON.stringify(value, (_, v) =>
    v && typeof v === 'object' && !Array.isArray(v)
      ? Object.fromEntries(Object.entries(v).sort(([a], [b]) => a.localeCompare(b)))
      : v);
}

/** The field errors of the API for one filter, e.g. `flag.filters[1].parameters`. */
export function filterErrors(errors: Record<string, string[]>, index: number): string[] {
  const prefix = `flag.filters[${index}]`;
  return Object.entries(errors)
    .filter(([key]) => key === prefix || key.startsWith(prefix + '.'))
    .flatMap(([, messages]) => messages);
}

/** The field errors of the API that are not about a filter. */
export function otherErrors(errors: Record<string, string[]>): string[] {
  return Object.entries(errors)
    .filter(([key]) => !key.startsWith('flag.filters['))
    .flatMap(([, messages]) => messages);
}
