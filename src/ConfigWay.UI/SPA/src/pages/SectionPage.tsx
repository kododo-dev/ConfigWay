import { useState, useCallback, useEffect, useMemo } from 'react';
import { useParams, Navigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import { useSections } from '../context/SectionsContext';
import SectionCard from '../components/settings/SectionCard';
import PageHeader from '../components/layout/PageHeader';
import { useTheme } from '@mui/material/styles';
import { saveSettings } from '../api/api';
import { collectFields, buildDraft, getChangedSettings, SENSITIVE_MASK, SENSITIVE_RESET } from '../utils/settings';
import type { ResetPatch } from '../utils/settings';
import { useI18n } from '../i18n/I18nContext';

const SectionPage = () => {
  const { sectionKey } = useParams<{ sectionKey: string }>();
  const { sections, loading, reload } = useSections();
  const { t } = useI18n();
  const theme = useTheme();

  const section = sections.find(s => s.key === decodeURIComponent(sectionKey ?? ''));

  const [draft,         setDraft]         = useState<Record<string, string>>({});
  const [keysToDelete,  setKeysToDelete]  = useState<string[]>([]);
  const [saving,        setSaving]        = useState(false);
  const [saveErrors,    setSaveErrors]    = useState<string[]>([]);
  const [search,        setSearch]        = useState('');

  useEffect(() => {
    if (section) {
      setDraft(buildDraft(section));
      setKeysToDelete([]);
      setSaveErrors([]);
    }
  }, [section]);

  const handleChange = useCallback((key: string, value: string) => {
    if (value === '__DELETE__') {
      setDraft(prev => {
        const next = { ...prev };
        delete next[key];
        return next;
      });
    } else {
      setDraft(prev => ({ ...prev, [key]: value }));
      setKeysToDelete(prev => prev.filter(k => k !== key));
    }
    setSaveErrors([]);
  }, []);

  const handleAdd = useCallback((patch: Record<string, string>) => {
    setDraft(prev => ({ ...prev, ...patch }));
    setSaveErrors([]);
  }, []);

  const handleRemove = useCallback((serverKeys: string[]) => {
    if (serverKeys.length > 0)
      setKeysToDelete(prev => [...prev, ...serverKeys]);
    setSaveErrors([]);
  }, []);

  const handleReset = useCallback(({ keysToDelete: resetKeys, draftPatch }: ResetPatch) => {
    setKeysToDelete(prev => {
      const next = [...prev];
      for (const k of resetKeys) {
        if (!next.includes(k)) next.push(k);
      }
      return next;
    });
    setDraft(prev => {
      const next = { ...prev };
      for (const [k, v] of Object.entries(draftPatch)) {
        if (v === '__DELETE__') {
          delete next[k];
        } else {
          next[k] = v;
        }
      }
      return next;
    });
    setSaveErrors([]);
  }, []);

  const original = useMemo(
    () => (section ? collectFields(section, section.key) : {}),
    [section]
  );
  const keysToDeleteSet = new Set(keysToDelete);

  const hasChanges = section
    ? (
        keysToDelete.length > 0 ||
        Object.entries(original).some(([k, v]) =>
          v === SENSITIVE_MASK
            ? draft[k] !== SENSITIVE_RESET
            : draft[k] !== (v ?? '')
        ) ||
        Object.keys(draft).some(k => !(k in original))
      )
    : false;

  const handleDiscard = useCallback(() => {
    if (section) {
      setDraft(buildDraft(section));
      setKeysToDelete([]);
      setSaveErrors([]);
    }
  }, [section]);

  const handleSave = useCallback(async () => {
    if (!section) return;
    setSaveErrors([]);
    const changed = getChangedSettings(
      collectFields(section, section.key),
      draft,
      keysToDeleteSet
    );

    if (changed.length === 0 && keysToDelete.length === 0) return;

    setSaving(true);

    try {
      const errors = await saveSettings(changed, keysToDelete);
      if (errors.length > 0) {
        setSaveErrors(errors);
      } else {
        setKeysToDelete([]);
        await reload();
      }
    } catch (e) {
      setSaveErrors([e instanceof Error ? e.message : t.unexpectedError]);
    } finally {
      setSaving(false);
    }
  }, [section, draft, keysToDelete, keysToDeleteSet, reload, t]);

  if (loading) return (
    <Box sx={{ display: 'flex', justifyContent: 'center', pt: 6 }}>
      <CircularProgress size={24} sx={{ color: theme.palette.primary.main }} />
    </Box>
  );

  if (!section) return <Navigate to="/" replace />;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <PageHeader
        title={section.name}
        onRefresh={reload}
        hasChanges={hasChanges}
        saving={saving}
        onSave={handleSave}
        onDiscard={handleDiscard}
        errors={saveErrors}
        searchQuery={search}
        onSearchChange={setSearch}
      />
      <Box sx={{ flex: 1, overflow: 'auto', p: 3 }}>
        <SectionCard
          section={section}
          prefix={section.key}
          draft={draft}
          onChange={handleChange}
          onAdd={handleAdd}
          onRemove={handleRemove}
          onReset={handleReset}
          searchQuery={search}
        />
      </Box>
    </Box>
  );
};

export default SectionPage;
