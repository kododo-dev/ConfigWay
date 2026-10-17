import type { WayApi } from '@kododo-dev/way-shell';
import type { Section, Setting } from './api.model';

// Errors come from the shell's API client: ApiError (problem details), ForbiddenError, SessionExpiredError.
export { ApiError } from '@kododo-dev/way-shell';

export const fetchConfiguration = (api: WayApi): Promise<Section[]> =>
  api.post<Section[]>('GetConfiguration');

/** Throws {@link ApiError} with the validation messages when the settings are not valid. */
export const saveSettings = (
  api: WayApi,
  settings: Setting[],
  keysToDelete: string[] = [],
): Promise<void> =>
  api.post<void>('UpdateConfiguration', { settings, keysToDelete });
