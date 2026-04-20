import type { ArrayField, ArrayItem, Section, Setting } from '../api/api.model';

// ── Key construction ──────────────────────────────────────────────────────────

/** Full key for a field within a section (prefix = full path of parent section). */
export function fieldFullKey(prefix: string, fieldKey: string): string {
  return `${prefix}:${fieldKey}`;
}

/** Full key for an array item (simple arrays: this IS the value key). */
export function itemFullKey(arrayPrefix: string, index: number): string {
  return `${arrayPrefix}:${index}`;
}

/** Full key for a field within a complex array item. */
export function itemFieldFullKey(arrayPrefix: string, index: number, fieldKey: string): string {
  return `${arrayPrefix}:${index}:${fieldKey}`;
}

// ── Field collection (builds full-key → value map) ────────────────────────────

/** Recursively collects all field values from a section tree. */
export function collectFields(section: Section, prefix: string): Record<string, string | null> {
  const result: Record<string, string | null> = {};

  for (const f of section.fields)
    result[fieldFullKey(prefix, f.key)] = f.value;

  for (const sub of section.sections)
    Object.assign(result, collectFields(sub, fieldFullKey(prefix, sub.key)));

  for (const arr of section.arrays)
    Object.assign(result, collectArrayFields(arr, fieldFullKey(prefix, arr.key)));

  return result;
}

function collectArrayFields(arr: ArrayField, arrayPrefix: string): Record<string, string | null> {
  const result: Record<string, string | null> = {};

  for (const item of arr.items) {
    if (arr.isSimple) {
      result[itemFullKey(arrayPrefix, item.index)] = item.value;
    } else {
      for (const f of item.fields)
        result[itemFieldFullKey(arrayPrefix, item.index, f.key)] = f.value;
      for (const sub of item.sections)
        Object.assign(result, collectFields(sub, `${arrayPrefix}:${item.index}:${sub.key}`));
      for (const nestedArr of item.arrays)
        Object.assign(result, collectArrayFields(nestedArr, `${arrayPrefix}:${item.index}:${nestedArr.key}`));
    }
  }

  return result;
}

// ── Draft building ────────────────────────────────────────────────────────────

export function buildDraft(section: Section): Record<string, string> {
  return Object.fromEntries(
    Object.entries(collectFields(section, section.key)).map(([k, v]) => [k, v ?? ''])
  );
}

export function buildDraftFromSections(sections: Section[]): Record<string, string> {
  const all: Record<string, string | null> = {};
  for (const s of sections) Object.assign(all, collectFields(s, s.key));
  return Object.fromEntries(Object.entries(all).map(([k, v]) => [k, v ?? '']));
}

// ── Changed settings ──────────────────────────────────────────────────────────

export function getChangedSettings(
  original: Record<string, string | null>,
  draft: Record<string, string>
): Setting[] {
  const result: Setting[] = [];

  // Modified existing keys
  for (const [k, v] of Object.entries(original)) {
    if (draft[k] !== (v ?? '')) {
      result.push({ key: k, value: draft[k] === '' ? null : (draft[k] ?? null) });
    }
  }

  // Newly added keys (from Add item — not present in original)
  for (const [k, v] of Object.entries(draft)) {
    if (!(k in original) && v !== '') {
      result.push({ key: k, value: v });
    }
  }

  return result;
}

// ── Array operations ──────────────────────────────────────────────────────────

/**
 * Returns all full keys that belong to an array item.
 * Used to compute keysToDelete when removing an item.
 */
export function collectItemKeys(
  arr: ArrayField,
  arrayPrefix: string,
  index: number
): string[] {
  if (arr.isSimple) return [itemFullKey(arrayPrefix, index)];

  const keys: string[] = [];
  const itemPrefix = itemFullKey(arrayPrefix, index);

  function walkTemplate(item: ArrayItem, prefix: string) {
    for (const f of item.fields)   keys.push(fieldFullKey(prefix, f.key));
    for (const sub of item.sections) {
      const subPrefix = fieldFullKey(prefix, sub.key);
      walkTemplate({ ...item, fields: sub.fields, sections: sub.sections, arrays: sub.arrays }, subPrefix);
    }
    for (const nestedArr of item.arrays) {
      const nestedPrefix = fieldFullKey(prefix, nestedArr.key);
      for (const ni of nestedArr.items)
        keys.push(...collectItemKeys(nestedArr, nestedPrefix, ni.index));
    }
  }

  // Use the template shape to know which sub-keys exist
  walkTemplate(arr.template, itemPrefix);
  return keys;
}

/**
 * Builds a draft patch for a new array item cloned from the template.
 * Returns a map of fullKey → '' (empty string, ready for editing).
 */
export function buildNewItemDraft(
  arr: ArrayField,
  arrayPrefix: string,
  newIndex: number
): Record<string, string> {
  if (arr.isSimple) return { [itemFullKey(arrayPrefix, newIndex)]: '' };

  const result: Record<string, string> = {};
  const itemPrefix = itemFullKey(arrayPrefix, newIndex);

  function walkTemplate(item: ArrayItem, prefix: string) {
    for (const f of item.fields)
      result[fieldFullKey(prefix, f.key)] = '';
    for (const sub of item.sections)
      walkTemplate({ ...item, fields: sub.fields, sections: sub.sections, arrays: sub.arrays }, fieldFullKey(prefix, sub.key));
  }

  walkTemplate(arr.template, itemPrefix);
  return result;
}

/**
 * Returns the next available index for a new array item.
 */
export function nextArrayIndex(arr: ArrayField): number {
  if (arr.items.length === 0) return 0;
  return Math.max(...arr.items.map(i => i.index)) + 1;
}
