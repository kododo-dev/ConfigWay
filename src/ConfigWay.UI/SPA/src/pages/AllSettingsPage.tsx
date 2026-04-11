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
import type { Section } from '../api/api.model';
import { collectFields, buildDraftFromSections, getChangedSettings } from '../utils/settings';

const AllSettingsPage = () => {
  const { sections, loading, error: loadError, reload } = useSections();
  const { t } = useI18n();
  const theme = useTheme();

  const [draft,      setDraft]      = useState<Record<string, string>>({});
  const [saving,     setSaving]     = useState(false);
  const [saveErrors, setSaveErrors] = useState<string[]>([]);
  const [search,     setSearch]     = useState('');

  useEffect(() => {
    setDraft(buildDraftFromSections(sections));
    setSaveErrors([]);
  }, [sections]);

  const handleChange = useCallback((key: string, value: string) => {
    setDraft(prev => ({ ...prev, [key]: value }));
    setSaveErrors([]);
  }, []);

  const hasChanges = sections.some(section =>
    Object.entries(collectFields(section)).some(([k, v]) => draft[k] !== (v ?? ''))
  );

  const handleDiscard = useCallback(() => {
    setDraft(buildDraftFromSections(sections));
    setSaveErrors([]);
  }, [sections]);

  const handleSave = useCallback(async () => {
    setSaveErrors([]);
    const changed = sections.flatMap(section =>
        getChangedSettings(collectFields(section), draft)
    );

    if (changed.length === 0) return;

    setSaving(true);
    
    try {
      const errors = await saveSettings(changed);
      if (errors.length > 0) {
        setSaveErrors(errors);
      } else {
        await reload();
      }
    } catch (e) {
      setSaveErrors([e instanceof Error ? e.message : t.unexpectedError]);
    } finally {
      setSaving(false);
    }
  }, [sections, draft, reload]);

  const hasNoResults = !loading && !!search && sections.every(s => {
    const q = search.toLowerCase();
    const anyMatch = (sec: Section): boolean =>
      sec.name.toLowerCase().includes(q) ||
      sec.fields.some(f =>
        f.name.toLowerCase().includes(q) ||
        f.key.toLowerCase().includes(q)  ||
        (draft[f.key] ?? '').toLowerCase().includes(q)
      ) ||
      sec.sections.some(anyMatch);
    return !anyMatch(s);
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
            draft={draft}
            onChange={handleChange}
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
