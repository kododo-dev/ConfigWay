import type { ArrayField, ArrayItem, Field, Section, Setting } from '../api/api.model';

export const SENSITIVE_MASK  = '***';
export const SENSITIVE_RESET = '__SENSITIVE_RESET__';

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
    Object.entries(collectFields(section, section.key))
      .map(([k, v]) => [k, v === SENSITIVE_MASK ? '' : (v ?? '')])
  );
}

export function buildDraftFromSections(sections: Section[]): Record<string, string> {
  const all: Record<string, string | null> = {};
  for (const s of sections) Object.assign(all, collectFields(s, s.key));
  return Object.fromEntries(
    Object.entries(all).map(([k, v]) => [k, v === SENSITIVE_MASK ? '' : (v ?? '')])
  );
}

// ── Changed settings ──────────────────────────────────────────────────────────

export function getChangedSettings(
  original: Record<string, string | null>,
  draft: Record<string, string>,
  keysToSkip?: Set<string>
): Setting[] {
  const result: Setting[] = [];

  for (const [k, v] of Object.entries(original)) {
    if (keysToSkip?.has(k)) continue;
    if (v === SENSITIVE_MASK) {
      if (draft[k] !== '' && draft[k] !== SENSITIVE_RESET) result.push({ key: k, value: draft[k] });
      continue;
    }
    if (draft[k] !== (v ?? '')) {
      result.push({ key: k, value: draft[k] === '' ? null : (draft[k] ?? null) });
    }
  }

  for (const [k, v] of Object.entries(draft)) {
    if (keysToSkip?.has(k)) continue;
    if (!(k in original) && v !== '') {
      result.push({ key: k, value: v });
    }
  }

  return result;
}

// ── Override detection ────────────────────────────────────────────────────────

/** Returns true if any field in the section (or its subsections/arrays) differs from its default. */
export function sectionHasOverrides(section: Section, prefix: string, draft: Record<string, string>): boolean {
  for (const f of section.fields) {
    const fk = fieldFullKey(prefix, f.key);
    if (f.isSensitive) {
      if (draft[fk] === SENSITIVE_RESET) continue;
      if (draft[fk] !== '') return true;
      if (f.value !== null) return true;
      continue;
    }
    if (draft[fk] !== (f.defaultValue ?? '')) return true;
  }
  for (const sub of section.sections) {
    if (sectionHasOverrides(sub, fieldFullKey(prefix, sub.key), draft)) return true;
  }
  for (const arr of section.arrays) {
    if (arrayHasOverrides(arr, fieldFullKey(prefix, arr.key), draft)) return true;
  }
  return false;
}

/** Returns true if any item in the array was added, deleted, or modified relative to defaults. */
export function arrayHasOverrides(arr: ArrayField, arrayPrefix: string, draft: Record<string, string>): boolean {
  for (const item of arr.items) {
    if (item.isDeletable) {
      // Deletable items are ConfigWay additions — their presence in draft is an override
      const key = itemFullKey(arrayPrefix, item.index);
      if (arr.isSimple) {
        if (key in draft) return true;
      } else {
        const keys = collectItemKeys(arr, arrayPrefix, item.index);
        if (keys.some(k => k in draft)) return true;
      }
    } else {
      // Non-deletable: check if value was overridden relative to base config default
      if (arr.isSimple) {
        const k = itemFullKey(arrayPrefix, item.index);
        if (draft[k] !== (item.defaultValue ?? '')) return true;
      } else {
        for (const f of item.fields) {
          const fk = itemFieldFullKey(arrayPrefix, item.index, f.key);
          if (f.isSensitive) {
            if (draft[fk] === SENSITIVE_RESET) continue;
            if (draft[fk] !== '') return true;
            if (f.value !== null) return true;
            continue;
          }
          if (draft[fk] !== (f.defaultValue ?? '')) return true;
        }
        for (const sub of item.sections) {
          if (sectionHasOverrides(sub, `${arrayPrefix}:${item.index}:${sub.key}`, draft)) return true;
        }
      }
    }
  }
  // Locally added items not yet saved
  const knownIndices = new Set(arr.items.map(i => i.index));
  return Object.keys(draft).some(k => {
    if (!k.startsWith(arrayPrefix + ':')) return false;
    const rest = k.slice(arrayPrefix.length + 1);
    const idx = parseInt(rest.split(':')[0], 10);
    return !isNaN(idx) && !knownIndices.has(idx);
  });
}

// ── Reset patch builders ──────────────────────────────────────────────────────

export interface ResetPatch {
  keysToDelete: string[];
  draftPatch: Record<string, string>;
}

/** Builds a reset patch for a single field. */
export function buildFieldResetPatch(fullKey: string, field: Field): ResetPatch {
  return {
    keysToDelete: [fullKey],
    draftPatch:   { [fullKey]: field.isSensitive ? SENSITIVE_RESET : (field.defaultValue ?? '') },
  };
}

/** Builds a reset patch for an entire section (all fields, subsections, arrays). */
export function buildSectionResetPatch(
  section: Section,
  prefix: string,
  draft: Record<string, string>
): ResetPatch {
  const keysToDelete: string[] = [];
  const draftPatch: Record<string, string> = {};

  for (const f of section.fields) {
    const fk = fieldFullKey(prefix, f.key);
    keysToDelete.push(fk);
    draftPatch[fk] = f.isSensitive ? SENSITIVE_RESET : (f.defaultValue ?? '');
  }

  for (const sub of section.sections) {
    const sub_ = buildSectionResetPatch(sub, fieldFullKey(prefix, sub.key), draft);
    keysToDelete.push(...sub_.keysToDelete);
    Object.assign(draftPatch, sub_.draftPatch);
  }

  for (const arr of section.arrays) {
    const arr_ = buildArrayResetPatch(arr, fieldFullKey(prefix, arr.key), draft);
    keysToDelete.push(...arr_.keysToDelete);
    Object.assign(draftPatch, arr_.draftPatch);
  }

  return { keysToDelete, draftPatch };
}

/** Builds a reset patch for an entire array (removes added items, resets modified base items). */
export function buildArrayResetPatch(
  arr: ArrayField,
  arrayPrefix: string,
  draft: Record<string, string>
): ResetPatch {
  const keysToDelete: string[] = [];
  const draftPatch: Record<string, string> = {};

  for (const item of arr.items) {
    if (item.isDeletable) {
      const keys = arr.isSimple
        ? [itemFullKey(arrayPrefix, item.index)]
        : collectItemKeys(arr, arrayPrefix, item.index);
      keysToDelete.push(...keys);
      keys.forEach(k => { draftPatch[k] = '__DELETE__'; });
    } else {
      if (arr.isSimple) {
        const k = itemFullKey(arrayPrefix, item.index);
        keysToDelete.push(k);
        draftPatch[k] = item.defaultValue ?? '';
      } else {
        for (const f of item.fields) {
          const fk = itemFieldFullKey(arrayPrefix, item.index, f.key);
          keysToDelete.push(fk);
          draftPatch[fk] = f.isSensitive ? SENSITIVE_RESET : (f.defaultValue ?? '');
        }
        for (const sub of item.sections) {
          const sub_ = buildSectionResetPatch(sub, `${arrayPrefix}:${item.index}:${sub.key}`, draft);
          keysToDelete.push(...sub_.keysToDelete);
          Object.assign(draftPatch, sub_.draftPatch);
        }
      }
    }
  }

  // Remove locally added items (in draft but not in arr.items)
  const knownIndices = new Set(arr.items.map(i => i.index));
  Object.keys(draft).forEach(k => {
    if (!k.startsWith(arrayPrefix + ':')) return;
    const rest = k.slice(arrayPrefix.length + 1);
    const idx = parseInt(rest.split(':')[0], 10);
    if (!isNaN(idx) && !knownIndices.has(idx)) {
      draftPatch[k] = '__DELETE__';
    }
  });

  return { keysToDelete, draftPatch };
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
