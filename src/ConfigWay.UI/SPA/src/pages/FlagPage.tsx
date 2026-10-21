import { useCallback, useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import FormControlLabel from '@mui/material/FormControlLabel';
import IconButton from '@mui/material/IconButton';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import RestoreIcon from '@mui/icons-material/Restore';
import { useShell } from '@kododo-dev/way-shell';
import { ApiError } from '../api/api';
import { resetFlag, saveFlag } from '../api/flags';
import type { FeatureFilter, FeatureFlag } from '../api/flags';
import PageHeader from '../components/layout/PageHeader';
import Panel, { MONO } from '../components/flags/Panel';
import { FilterEditor } from '../components/flags/FilterEditors';
import FlagCheck from '../components/flags/FlagCheck';
import { useFlags } from '../context/FlagsContext';
import { useI18n } from '../i18n/I18nContext';
import { filterErrors, filterKind, newFilter, newFlag, otherErrors, stableJson } from '../utils/flags';
import type { FilterKind } from '../utils/flags';

const HINT = { ...MONO, fontSize: '0.72rem', color: 'text.secondary' } as const;

/** What a save compares and sends: an empty description is none. */
const normalize = (flag: FeatureFlag): FeatureFlag => ({
  ...flag,
  description: flag.description?.trim() ? flag.description.trim() : null,
});

/** The editor of one flag (`flag/:flagId`) or of a new one (`flags/new`). */
const FlagPage = ({ isNew = false }: { isNew?: boolean }) => {
  const { flagId } = useParams<{ flagId: string }>();
  const { data, loading, reload } = useFlags();
  const { api, navigate, wayId } = useShell();
  const { t } = useI18n();

  const id = decodeURIComponent(flagId ?? '');
  const item = isNew ? null : data?.flags.find(f => f.id.toLowerCase() === id.toLowerCase()) ?? null;
  const original = useMemo(() => (item ? item.flag : newFlag()), [item]);

  const [draft, setDraft] = useState<FeatureFlag>(original);
  const [invalidJson, setInvalidJson] = useState<boolean[]>([]);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [messages, setMessages] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const [addMenu, setAddMenu] = useState<HTMLElement | null>(null);

  useEffect(() => {
    setDraft(original);
    setInvalidJson([]);
    setErrors({});
    setMessages([]);
  }, [original]);

  // A flag that no longer exists, e.g. deleted elsewhere: back to the list.
  useEffect(() => {
    if (!isNew && !loading && data && !item) navigate(wayId, 'flags', { replace: true });
  }, [isNew, loading, data, item, navigate, wayId]);

  const hasChanges = stableJson(normalize(draft)) !== stableJson(normalize(original));
  const canSave = (isNew || hasChanges) && !invalidJson.some(Boolean);

  // The API's errors are about the values sent; an edit clears them until the next save.
  const change = useCallback((patch: Partial<FeatureFlag>) => {
    setDraft(prev => ({ ...prev, ...patch }));
    setErrors({});
    setMessages([]);
  }, []);

  const setFilter = (index: number, filter: FeatureFilter) =>
    change({ filters: draft.filters.map((f, i) => (i === index ? filter : f)) });

  const addFilter = (kind: FilterKind) => {
    setAddMenu(null);
    change({ filters: [...draft.filters, newFilter(kind)] });
  };

  const removeFilter = (index: number) => {
    change({ filters: draft.filters.filter((_, i) => i !== index) });
    setInvalidJson(prev => prev.filter((_, i) => i !== index));
  };

  const fail = (e: unknown) => {
    if (e instanceof ApiError && Object.keys(e.errors).length > 0) {
      setErrors(e.errors);
      setMessages(otherErrors(e.errors).filter(m => !(e.errors['flag.id'] ?? []).includes(m)));
    } else {
      setErrors({});
      setMessages([e instanceof ApiError ? e.messages.join(' ') : e instanceof Error ? e.message : t.unexpectedError]);
    }
  };

  const save = async () => {
    if (!canSave) return;
    setSaving(true);
    setErrors({});
    setMessages([]);
    try {
      const flag = normalize(draft);
      await saveFlag(api, flag, isNew);
      await reload();
      if (isNew) navigate(wayId, `flag/${encodeURIComponent(flag.id)}`, { replace: true });
    } catch (e) {
      fail(e);
    } finally {
      setSaving(false);
    }
  };

  const discard = () => {
    setDraft(original);
    setInvalidJson([]);
    setErrors({});
    setMessages([]);
  };

  const reset = async () => {
    if (!item) return;
    setConfirm(false);
    setSaving(true);
    try {
      await resetFlag(api, item.id);
      await reload();
      if (!item.configured) navigate(wayId, 'flags', { replace: true });
    } catch (e) {
      fail(e);
    } finally {
      setSaving(false);
    }
  };

  if (!isNew && (loading || !item)) return (
    <Box sx={{ display: 'flex', justifyContent: 'center', pt: 6 }}>
      <CircularProgress size={24} />
    </Box>
  );

  const kindLabel = (kind: FilterKind) => ({
    Percentage: t.filterPercentage,
    TimeWindow: t.filterTimeWindow,
    Targeting: t.filterTargeting,
    Custom: t.filterCustom,
  })[kind];

  const source = !item ? null
    : !item.configured ? t.sourceConfigWayOnly
    : item.hasOverride ? t.sourceOverride
    : t.sourceConfigured;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <PageHeader
        title={isNew ? t.newFlag : item!.id}
        onRefresh={isNew ? undefined : reload}
        hasChanges={canSave}
        saving={saving}
        onSave={save}
        onDiscard={hasChanges ? discard : undefined}
        errors={messages}
      />

      <Box sx={{ flex: 1, overflow: 'auto', p: 3 }}>
        <Box sx={{ maxWidth: 900, display: 'flex', flexDirection: 'column', gap: 2 }}>
          {item?.error && <Alert severity="error" sx={{ fontSize: '0.78rem' }}>{t.overrideError(item.error)}</Alert>}
          {item?.hasVariants && <Alert severity="info" sx={{ fontSize: '0.78rem' }}>{t.variantsKept}</Alert>}
          {item?.isDotnetSchema && <Alert severity="info" sx={{ fontSize: '0.78rem' }}>{t.dotnetSchemaNote}</Alert>}

          <Panel
            title={t.flagSection}
            actions={item?.hasOverride && (
              <Button
                size="small"
                startIcon={item.configured ? <RestoreIcon /> : <DeleteOutlinedIcon />}
                color={item.configured ? 'primary' : 'error'}
                onClick={() => setConfirm(true)}
                disabled={saving}
                sx={{ ...MONO, fontSize: '0.72rem', textTransform: 'none' }}
              >
                {item.configured ? t.resetToConfigured : t.deleteFlag}
              </Button>
            )}
          >
            <TextField
              label={t.flagId}
              size="small"
              value={draft.id}
              onChange={e => change({ id: e.target.value })}
              disabled={!isNew}
              error={!!errors['flag.id']}
              helperText={errors['flag.id']?.join(' ') ?? t.flagIdHint}
              autoFocus={isNew}
              slotProps={{ htmlInput: { spellCheck: false, 'data-testid': 'flag-id' } }}
            />
            <TextField
              label={t.flagDescription}
              size="small"
              value={draft.description ?? ''}
              onChange={e => change({ description: e.target.value })}
              multiline
            />
            <FormControlLabel
              control={<Switch checked={draft.enabled} onChange={e => change({ enabled: e.target.checked })} />}
              label={<Typography sx={{ ...MONO, fontSize: '0.8rem' }}>{t.flagEnabled}</Typography>}
            />
            {source && <Typography sx={HINT}>{source}</Typography>}
          </Panel>

          <Panel
            title={t.conditions}
            actions={(
              <>
                <Button
                  size="small"
                  startIcon={<AddIcon />}
                  onClick={e => setAddMenu(e.currentTarget)}
                  sx={{ ...MONO, fontSize: '0.72rem', textTransform: 'none' }}
                >
                  {t.addCondition}
                </Button>
                <Menu anchorEl={addMenu} open={addMenu !== null} onClose={() => setAddMenu(null)}>
                  {(['Percentage', 'TimeWindow', 'Targeting', 'Custom'] as const).map(kind => (
                    <MenuItem key={kind} onClick={() => addFilter(kind)} sx={{ ...MONO, fontSize: '0.8rem' }}>
                      {kindLabel(kind)}
                    </MenuItem>
                  ))}
                </Menu>
              </>
            )}
          >
            {!draft.enabled && <Alert severity="info" sx={{ fontSize: '0.75rem' }}>{t.disabledHint}</Alert>}
            {draft.filters.length === 0 && <Typography sx={HINT}>{t.noConditions}</Typography>}

            {draft.filters.length > 1 && (
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                <Typography sx={{ ...MONO, fontSize: '0.78rem' }}>{t.requirementLabel}</Typography>
                <ToggleButtonGroup
                  size="small"
                  exclusive
                  value={draft.requirementType}
                  onChange={(_, value) => value && change({ requirementType: value })}
                >
                  <ToggleButton value="Any" sx={{ ...MONO, fontSize: '0.72rem', textTransform: 'none', py: 0.25 }}>
                    {t.requirementAny}
                  </ToggleButton>
                  <ToggleButton value="All" sx={{ ...MONO, fontSize: '0.72rem', textTransform: 'none', py: 0.25 }}>
                    {t.requirementAll}
                  </ToggleButton>
                </ToggleButtonGroup>
                <Typography sx={{ ...MONO, fontSize: '0.78rem' }}>{t.requirementSuffix}</Typography>
              </Box>
            )}

            {draft.filters.map((filter, index) => {
              const kind = filterKind(filter.name);
              const messagesOfFilter = filterErrors(errors, index);
              return (
                <Panel
                  key={index}
                  invalid={messagesOfFilter.length > 0}
                  title={(
                    <>
                      {kindLabel(kind)}
                      {kind !== 'Custom' && (
                        <Typography component="span" sx={{ ...HINT, ml: 1, fontWeight: 400 }}>{filter.name}</Typography>
                      )}
                    </>
                  )}
                  actions={(
                    <Tooltip title={t.removeCondition}>
                      <IconButton size="small" onClick={() => removeFilter(index)}>
                        <DeleteOutlinedIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                >
                  {messagesOfFilter.length > 0 && (
                    <Alert severity="error" sx={{ fontSize: '0.75rem' }}>{messagesOfFilter.join(' ')}</Alert>
                  )}
                  <FilterEditor
                    filter={filter}
                    onChange={next => setFilter(index, next)}
                    registered={data?.filters ?? []}
                    onValidity={valid => setInvalidJson(prev => {
                      const next = [...prev];
                      next[index] = !valid;
                      return next;
                    })}
                  />
                </Panel>
              );
            })}
          </Panel>

          {!isNew && item && <FlagCheck flagId={item.id} hasChanges={hasChanges} />}
        </Box>
      </Box>

      <Dialog open={confirm} onClose={() => setConfirm(false)}>
        <DialogTitle sx={{ ...MONO, fontSize: '0.95rem' }}>
          {item?.configured ? t.confirmResetTitle : t.confirmDeleteTitle}
        </DialogTitle>
        <DialogContent>
          <DialogContentText sx={{ ...MONO, fontSize: '0.8rem' }}>
            {item && (item.configured ? t.confirmResetText(item.id) : t.confirmDeleteText(item.id))}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirm(false)} sx={{ ...MONO, textTransform: 'none' }}>{t.cancel}</Button>
          <Button
            onClick={reset}
            color={item?.configured ? 'primary' : 'error'}
            variant="contained"
            disableElevation
            sx={{ ...MONO, textTransform: 'none' }}
          >
            {item?.configured ? t.resetToConfigured : t.deleteFlag}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default FlagPage;
