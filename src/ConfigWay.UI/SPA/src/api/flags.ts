import type { WayApi } from '@kododo-dev/way-shell';

// The feature flags API of Kododo.ConfigWay.FeatureManagement, under the editor's API when the package is added.

export type RequirementType = 'Any' | 'All';

export type FilterParameters = Record<string, unknown>;

export interface FeatureFilter {
  name: string;
  parameters: FilterParameters | null;
}

export interface FeatureFlag {
  id: string;
  enabled: boolean;
  requirementType: RequirementType;
  filters: FeatureFilter[];
  description: string | null;
}

export interface FeatureFlagItem {
  id: string;
  /** The flag in effect: the override when there is one, otherwise the configured flag. */
  flag: FeatureFlag;
  /** The flag as the application's configuration defines it; null when only ConfigWay has it. */
  configured: FeatureFlag | null;
  hasOverride: boolean;
  isDotnetSchema: boolean;
  hasVariants: boolean;
  /** Why the stored override cannot be read. */
  error: string | null;
}

export interface FeatureFlags {
  isFeatureManagementRegistered: boolean;
  /** Aliases of the filters the application registered. */
  filters: string[];
  flags: FeatureFlagItem[];
}

export interface FlagEvaluation {
  enabled: boolean;
  variant: string | null;
}

export interface Capabilities {
  extensions: string[];
}

export const fetchCapabilities = (api: WayApi): Promise<Capabilities> =>
  api.post<Capabilities>('GetCapabilities');

export const fetchFlags = (api: WayApi): Promise<FeatureFlags> =>
  api.post<FeatureFlags>('GetFeatureFlags');

/** Throws ApiError with field errors such as `flag.id` or `flag.filters[1].parameters`. */
export const saveFlag = (api: WayApi, flag: FeatureFlag, isNew = false): Promise<void> =>
  api.post<void>('SaveFeatureFlag', { flag, isNew });

export const resetFlag = (api: WayApi, id: string): Promise<void> =>
  api.post<void>('ResetFeatureFlag', { id });

export const evaluateFlag = (
  api: WayApi,
  id: string,
  userId: string | null,
  groups: string[],
): Promise<FlagEvaluation> =>
  api.post<FlagEvaluation>('EvaluateFeatureFlag', { id, userId, groups });
