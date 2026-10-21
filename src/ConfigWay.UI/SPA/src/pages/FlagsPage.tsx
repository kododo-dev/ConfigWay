import { useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Snackbar from '@mui/material/Snackbar';
import Switch from '@mui/material/Switch';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import AddIcon from '@mui/icons-material/Add';
import ErrorOutlinedIcon from '@mui/icons-material/ErrorOutlined';
import { useTheme } from '@mui/material/styles';
import { useShell } from '@kododo-dev/way-shell';
import { ApiError } from '../api/api';
import { resetFlag, saveFlag } from '../api/flags';
import type { FeatureFlag, FeatureFlagItem } from '../api/flags';
import PageHeader from '../components/layout/PageHeader';
import Highlight from '../components/common/Highlight';
import { MONO } from '../components/flags/Panel';
import { flagSummary } from '../components/flags/flagSummary';
import { useFlags } from '../context/FlagsContext';
import { useI18n } from '../i18n/I18nContext';

interface Toggled {
  previous: FeatureFlag;
  /** Without an override before the switch, undo removes the override instead of saving one. */
  hadOverride: boolean;
  message: string;
}

/** The list of feature flags, with a switch to turn each on or off at once. */
const FlagsPage = () => {
  const { data, loading, error, reload } = useFlags();
  const { api, navigate, wayId } = useShell();
  const { t, lang } = useI18n();
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';

  const [search, setSearch] = useState('');
  const [pending, setPending] = useState<string | null>(null);
  const [toggled, setToggled] = useState<Toggled | null>(null);
  const [toggleError, setToggleError] = useState<string | null>(null);

  const run = async (id: string, action: () => Promise<void>) => {
    setPending(id);
    setToggleError(null);
    try {
      await action();
      await reload();
      return true;
    } catch (e) {
      setToggleError(e instanceof ApiError ? e.messages.join(' ') : e instanceof Error ? e.message : t.unexpectedError);
      return false;
    } finally {
      setPending(null);
    }
  };

  const toggle = async (item: FeatureFlagItem) => {
    const next = { ...item.flag, enabled: !item.flag.enabled };
    if (await run(item.id, () => saveFlag(api, next)))
      setToggled({
        previous: item.flag,
        hadOverride: item.hasOverride,
        message: next.enabled ? t.flagTurnedOn(item.id) : t.flagTurnedOff(item.id),
      });
  };

  const undo = async () => {
    if (!toggled) return;
    const { previous, hadOverride } = toggled;
    setToggled(null);
    await run(previous.id, () => (hadOverride ? saveFlag(api, previous) : resetFlag(api, previous.id)));
  };

  const q = search.trim().toLowerCase();
  const items = (data?.flags ?? []).filter(i =>
    !q || i.id.toLowerCase().includes(q) || (i.flag.description ?? '').toLowerCase().includes(q));

  const open = (id: string) => navigate(wayId, `flag/${encodeURIComponent(id)}`);
  const rowBorder = isDark ? '#2e2e2e' : '#e0e0e0';

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <PageHeader
        title={t.featureFlags}
        onRefresh={reload}
        searchQuery={search}
        onSearchChange={setSearch}
        searchPlaceholder={t.flagsSearchPlaceholder}
        actions={(
          <Button
            size="small"
            variant="outlined"
            startIcon={<AddIcon sx={{ fontSize: '14px !important' }} />}
            onClick={() => navigate(wayId, 'flags/new')}
            sx={{ ...MONO, fontSize: '0.72rem', textTransform: 'none', flexShrink: 0 }}
          >
            {t.newFlag}
          </Button>
        )}
        errors={[error, toggleError].filter((e): e is string => !!e)}
      />

      <Box sx={{ flex: 1, overflow: 'auto', p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
        {data && !data.isFeatureManagementRegistered && (
          <Alert severity="warning" sx={{ fontSize: '0.78rem' }}>{t.featureManagementMissing}</Alert>
        )}

        {loading && !data && (
          <Box sx={{ display: 'flex', justifyContent: 'center', pt: 6 }}>
            <CircularProgress size={24} />
          </Box>
        )}

        {data && data.flags.length === 0 && (
          <Typography sx={{ ...MONO, fontSize: '0.8rem', color: 'text.secondary' }}>{t.noFlags}</Typography>
        )}

        {data && data.flags.length > 0 && items.length === 0 && (
          <Typography sx={{ ...MONO, fontSize: '0.8rem', color: 'text.secondary' }}>
            {t.noResults} "{search}"
          </Typography>
        )}

        {items.length > 0 && (
          <Box sx={{
            border: `1px solid ${rowBorder}`,
            borderRadius: '6px',
            overflow: 'hidden',
            background: isDark ? '#242424' : '#fff',
          }}>
            {items.map((item, index) => (
              <Box
                key={item.id}
                data-testid={`flag-row-${item.id}`}
                onClick={() => open(item.id)}
                sx={{
                  display: 'flex', alignItems: 'center', gap: 1.5,
                  px: 2, py: 1.25,
                  borderTop: index === 0 ? 'none' : `1px solid ${rowBorder}`,
                  cursor: 'pointer',
                  '&:hover': { background: isDark ? '#2a2a2a' : '#fafafa' },
                }}
              >
                <Box onClick={e => e.stopPropagation()} sx={{ flexShrink: 0 }}>
                  <Tooltip title={item.flag.enabled ? t.flagOn : t.flagOff}>
                    <span>
                      <Switch
                        size="small"
                        checked={item.flag.enabled}
                        disabled={pending !== null || item.error !== null}
                        onChange={() => toggle(item)}
                        slotProps={{ input: { 'aria-label': item.id } }}
                      />
                    </span>
                  </Tooltip>
                </Box>

                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                    <Typography sx={{ ...MONO, fontSize: '0.82rem', fontWeight: 600 }}>
                      <Highlight text={item.id} query={search} />
                    </Typography>
                    {item.hasOverride && item.configured && <Badge label={t.badgeOverride} color="primary" />}
                    {!item.configured && <Badge label={t.badgeConfigWayOnly} color="secondary" />}
                    {item.isDotnetSchema && <Badge label={t.badgeDotnetSchema} />}
                    {item.hasVariants && <Badge label={t.badgeVariants} />}
                    {item.error && (
                      <Tooltip title={item.error}>
                        <ErrorOutlinedIcon color="error" sx={{ fontSize: 16 }} />
                      </Tooltip>
                    )}
                  </Box>
                  <Typography sx={{
                    ...MONO, fontSize: '0.72rem', color: 'text.secondary', mt: 0.25,
                    overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                  }}>
                    {flagSummary(item.flag, t, lang)}
                    {item.flag.description && ` · ${item.flag.description}`}
                  </Typography>
                </Box>
              </Box>
            ))}
          </Box>
        )}
      </Box>

      <Snackbar
        open={toggled !== null}
        autoHideDuration={6000}
        onClose={(_, reason) => { if (reason !== 'clickaway') setToggled(null); }}
        message={toggled?.message}
        action={(
          <Button size="small" color="primary" onClick={undo} sx={{ ...MONO, textTransform: 'none' }}>
            {t.undo}
          </Button>
        )}
      />
    </Box>
  );
};

const Badge = ({ label, color }: { label: string; color?: 'primary' | 'secondary' }) => (
  <Chip
    label={label}
    size="small"
    variant="outlined"
    color={color ?? 'default'}
    sx={{ height: 18, '& .MuiChip-label': { px: 0.75, fontSize: '0.65rem' } }}
  />
);

export default FlagsPage;
