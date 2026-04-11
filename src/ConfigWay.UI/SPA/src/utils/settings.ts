import type { Section, Setting } from '../api/api.model';

/** Recursively collects all fields from a section and its subsections. */
export function collectFields(section: Section): Record<string, string | null> {
  const result: Record<string, string | null> = {};
  for (const f of section.fields) result[f.key] = f.value;
  for (const sub of section.sections) Object.assign(result, collectFields(sub));
  return result;
}

/** Builds a flat draft map from a single section (for SectionPage). */
export function buildDraft(section: Section): Record<string, string> {
  return Object.fromEntries(
    Object.entries(collectFields(section)).map(([k, v]) => [k, v ?? ''])
  );
}

/** Builds a flat draft map across multiple sections (for AllSettingsPage). */
export function buildDraftFromSections(sections: Section[]): Record<string, string> {
  const all: Record<string, string | null> = {};
  for (const s of sections) Object.assign(all, collectFields(s));
  return Object.fromEntries(Object.entries(all).map(([k, v]) => [k, v ?? '']));
}

/** Returns only the settings that differ from their original values. */
export function getChangedSettings(
  original: Record<string, string | null>,
  draft: Record<string, string>
): Setting[] {
  return Object.entries(original)
    .filter(([k, v]) => draft[k] !== (v ?? ''))
    .map(([k]) => ({
      key: k,
      value: draft[k] === '' ? null : draft[k],
    }));
}
