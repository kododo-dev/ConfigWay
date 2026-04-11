import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { fetchConfiguration } from '../api/api';
import type { Section } from '../api/api.model';

interface SectionsContextValue {
  sections: Section[];
  loading: boolean;
  error: string | null;
  reload: () => Promise<void>;
}

const SectionsContext = createContext<SectionsContextValue>({
  sections: [],
  loading: false,
  error: null,
  reload: async () => {},
});

export const SectionsProvider = ({ children }: { children: React.ReactNode }) => {
  const [sections, setSections] = useState<Section[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      setSections(await fetchConfiguration());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { reload(); }, [reload]);

  return (
    <SectionsContext.Provider value={{ sections, loading, error, reload }}>
      {children}
    </SectionsContext.Provider>
  );
};

export const useSections = () => useContext(SectionsContext);
