import { useState, useCallback, useEffect } from 'react';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import Typography from '@mui/material/Typography';
import { useSections } from '../context/SectionsContext';
import SectionCard from '../components/settings/SectionCard';
import PageHeader from '../components/layout/PageHeader';
import { useI18n } from '../i18n/I18nContext';
import { useTheme } from '@mui/material/styles';
import { saveSettings } from '../api/api';
import { collectFields, buildDraftFromSections, getChangedSettings } from '../utils/settings';

const AllSettingsPage = () => {
  const { sections, loading, error: loadError, reload } = useSections();
  const { t } = useI18n();
  const theme = useTheme();

  const [draft,         setDraft]         = useState<Record<string, string>>({});
  const [keysToDelete,  setKeysToDelete]  = useState<string[]>([]);
  const [saving,        setSaving]        = useState(false);
  const [saveErrors,    setSaveErrors]    = useState<string[]>([]);
  const [search,        setSearch]        = useState('');

  useEffect(() => {
    setDraft(buildDraftFromSections(sections));
    setKeysToDelete([]);
    setSaveErrors([]);
  }, [sections]);

  const handleChange = useCallback((key: string, value: string) => {
    if (value === '__DELETE__') {
      setDraft(prev => {
        const next = { ...prev };
        delete next[key];
        return next;
      });
    } else {
      setDraft(prev => ({ ...prev, [key]: value }));
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

  const hasChanges =
    keysToDelete.length > 0 ||
    sections.some(section => {
      const original = collectFields(section, section.key);
      return (
        Object.entries(original).some(([k, v]) => draft[k] !== (v ?? '')) ||
        Object.keys(draft).some(k => !(k in original))
      );
    });

  const handleDiscard = useCallback(() => {
    setDraft(buildDraftFromSections(sections));
    setKeysToDelete([]);
    setSaveErrors([]);
  }, [sections]);

  const handleSave = useCallback(async () => {
    setSaveErrors([]);
    const changed = sections.flatMap(section =>
      getChangedSettings(collectFields(section, section.key), draft)
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
  }, [sections, draft, keysToDelete, reload, t]);

  const hasNoResults = !loading && !!search && sections.every(s => {
    const q = search.toLowerCase();
    const anyMatch = (sec: typeof s, prefix: string): boolean =>
      sec.name.toLowerCase().includes(q) ||
      sec.fields.some(f =>
        f.name.toLowerCase().includes(q) ||
        f.key.toLowerCase().includes(q)  ||
        (draft[`${prefix}:${f.key}`] ?? '').toLowerCase().includes(q)
      ) ||
      sec.sections.some(sub => anyMatch(sub, `${prefix}:${sub.key}`)) ||
      sec.arrays.some(arr => arr.name.toLowerCase().includes(q));
    return !anyMatch(s, s.key);
  });

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <PageHeader
        title={t.allSettingsTitle}
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
        {loading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', pt: 6 }}>
            <CircularProgress size={24} sx={{ color: theme.palette.primary.main }} />
          </Box>
        )}
        {loadError && (
          <Box sx={{
            fontFamily: 'monospace', fontSize: '0.8rem',
            color: theme.palette.error.main,
            p: 2,
            background: theme.palette.mode === 'dark' ? '#2a1a1a' : '#fff5f5',
            borderRadius: 1, mb: 2,
          }}>
            {loadError}
          </Box>
        )}
        {!loading && !loadError && sections.length === 0 && (
          <Box sx={{ textAlign: 'center', pt: 8 }}>
            <Typography sx={{ fontFamily: 'monospace', color: theme.palette.text.secondary, fontSize: '0.82rem' }}>
              {t.noSectionsMessage}
            </Typography>
          </Box>
        )}

        {!loading && sections.map(section => (
          <SectionCard
            key={section.key}
            section={section}
            prefix={section.key}
            draft={draft}
            onChange={handleChange}
            onAdd={handleAdd}
            onRemove={handleRemove}
            searchQuery={search}
          />
        ))}

        {hasNoResults && (
          <Box sx={{ textAlign: 'center', pt: 6 }}>
            <Typography sx={{ fontFamily: 'monospace', color: theme.palette.text.secondary, fontSize: '0.82rem' }}>
              {t.noResults} &ldquo;{search}&rdquo;
            </Typography>
          </Box>
        )}
      </Box>
    </Box>
  );
};

export default AllSettingsPage;
