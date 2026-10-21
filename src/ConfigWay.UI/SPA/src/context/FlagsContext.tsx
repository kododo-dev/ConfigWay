import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { useShell } from '@kododo-dev/way-shell';
import { fetchCapabilities, fetchFlags } from '../api/flags';
import type { FeatureFlags } from '../api/flags';

interface FlagsContextValue {
  /** Whether the application added the feature flags package (Kododo.ConfigWay.FeatureManagement). */
  available: boolean;
  data: FeatureFlags | null;
  loading: boolean;
  error: string | null;
  reload: () => Promise<void>;
}

const FlagsContext = createContext<FlagsContextValue>({
  available: false,
  data: null,
  loading: false,
  error: null,
  reload: async () => {},
});

// The flags are loaded only when the server reports the extension, so the editor works the same without it.
export const FlagsProvider = ({ children }: { children: React.ReactNode }) => {
  const { api } = useShell();
  const [available, setAvailable] = useState(false);
  const [data, setData] = useState<FeatureFlags | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      setData(await fetchFlags(api));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load');
    } finally {
      setLoading(false);
    }
  }, [api]);

  useEffect(() => {
    let cancelled = false;
    fetchCapabilities(api)
      .then(c => {
        if (cancelled) return;
        const has = c.extensions.includes('flags');
        setAvailable(has);
        if (has) load();
        else setLoading(false);
      })
      .catch(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [api, load]);

  return (
    <FlagsContext.Provider value={{ available, data, loading, error, reload: load }}>
      {children}
    </FlagsContext.Provider>
  );
};

export const useFlags = () => useContext(FlagsContext);
