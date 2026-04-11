import { useState, useCallback, useEffect } from 'react';
import { useParams, Navigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import { useSections } from '../context/SectionsContext';
import SectionCard from '../components/settings/SectionCard';
import PageHeader from '../components/layout/PageHeader';
import { useTheme } from '@mui/material/styles';
import { saveSettings } from '../api/api';
import { collectFields, buildDraft, getChangedSettings } from '../utils/settings';
import {useI18n} from "../i18n/I18nContext.tsx";

const SectionPage = () => {
  const { sectionKey } = useParams<{ sectionKey: string }>();
  const { sections, loading, reload } = useSections();
  const { t } = useI18n();
  const theme = useTheme();

  const section = sections.find(s => s.key === decodeURIComponent(sectionKey ?? ''));

  const [draft,      setDraft]      = useState<Record<string, string>>({});
  const [saving,     setSaving]     = useState(false);
  const [saveErrors, setSaveErrors] = useState<string[]>([]);
  const [search,     setSearch]     = useState('');

  useEffect(() => {
    if (section) { setDraft(buildDraft(section)); setSaveErrors([]); }
  }, [section]);

  const handleChange = useCallback((key: string, value: string) => {
    setDraft(prev => ({ ...prev, [key]: value }));
    setSaveErrors([]);
  }, []);

  const hasChanges = section
    ? Object.entries(collectFields(section)).some(([k, v]) => draft[k] !== (v ?? ''))
    : false;

  const handleDiscard = useCallback(() => {
    if (section) { setDraft(buildDraft(section)); setSaveErrors([]); }
  }, [section]);

  const handleSave = useCallback(async () => {
    if (!section) return;
    setSaveErrors([]);
    const changed = getChangedSettings(collectFields(section), draft);

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
  }, [section, draft, reload]);

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
          draft={draft}
          onChange={handleChange}
          searchQuery={search}
        />
      </Box>
    </Box>
  );
};

export default SectionPage;
