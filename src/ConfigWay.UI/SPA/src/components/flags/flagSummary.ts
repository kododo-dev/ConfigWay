import type { FeatureFilter, FeatureFlag } from '../../api/flags';
import type { TranslationKeys } from '../../i18n/translations';
import { asNumber, asText, filterKind, formatTime, readAudience } from '../../utils/flags';

/** A short description of when the flag is on, for the list of flags. */
export function flagSummary(flag: FeatureFlag, t: TranslationKeys, lang: string): string {
  if (!flag.enabled) return t.flagOff;
  if (flag.filters.length === 0) return t.summaryEveryone;
  return flag.filters
    .map(f => filterSummary(f, t, lang))
    .join(flag.requirementType === 'All' ? t.summaryAnd : t.summaryOr);
}

function filterSummary(filter: FeatureFilter, t: TranslationKeys, lang: string): string {
  const parameters = filter.parameters;
  switch (filterKind(filter.name)) {
    case 'Percentage':
      return t.summaryPercentage(asNumber(parameters?.Value) ?? '?');
    case 'TimeWindow': {
      const start = asText(parameters?.Start);
      const end = asText(parameters?.End);
      const parts = [
        start ? t.summaryFrom(formatTime(start, lang)) : null,
        end ? t.summaryUntil(formatTime(end, lang)) : null,
        parameters?.Recurrence ? `(${t.summaryRepeats})` : null,
      ].filter(Boolean);
      return parts.join(' ');
    }
    case 'Targeting': {
      const audience = readAudience(parameters);
      const parts = [
        audience.Users.length > 0 ? t.summaryUsers(audience.Users.length) : null,
        audience.Groups.length > 0 ? t.summaryGroups(audience.Groups.length) : null,
        audience.DefaultRolloutPercentage ? t.summaryDefaultRollout(audience.DefaultRolloutPercentage) : null,
      ].filter(Boolean);
      return parts.length > 0 ? parts.join(' · ') : t.summaryNobody;
    }
    case 'Custom':
      return filter.name;
  }
}
