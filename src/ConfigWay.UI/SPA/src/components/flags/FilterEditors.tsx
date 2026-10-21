import { useEffect, useState } from 'react';
import Alert from '@mui/material/Alert';
import Autocomplete from '@mui/material/Autocomplete';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Slider from '@mui/material/Slider';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import type { FeatureFilter, FilterParameters } from '../../api/flags';
import { useI18n } from '../../i18n/I18nContext';
import {
  asNumber, asText, filterKind, fromLocalInput, isRegisteredFilter, readAudience, toLocalInput, windowState,
} from '../../utils/flags';
import type { Audience } from '../../utils/flags';
import ChipsInput from './ChipsInput';
import { MONO } from './Panel';

const HINT = { ...MONO, fontSize: '0.72rem', color: 'text.secondary' } as const;

interface EditorProps {
  filter: FeatureFilter;
  onChange: (filter: FeatureFilter) => void;
}

/** The form for the filter's parameters: one per built-in filter, JSON for the others. */
export const FilterEditor = ({ filter, onChange, registered, onValidity }: EditorProps & {
  registered: string[];
  onValidity: JsonValidity;
}) => {
  switch (filterKind(filter.name)) {
    case 'Percentage': return <PercentageEditor filter={filter} onChange={onChange} />;
    case 'TimeWindow': return <TimeWindowEditor filter={filter} onChange={onChange} />;
    case 'Targeting':  return <TargetingEditor filter={filter} onChange={onChange} />;
    case 'Custom':
      return <CustomFilterEditor filter={filter} onChange={onChange} registered={registered} onValidity={onValidity} />;
  }
};

// ── Percentage ─────────────────────────────────────────────────────────────

const PercentageEditor = ({ filter, onChange }: EditorProps) => {
  const { t } = useI18n();
  const value = asNumber(filter.parameters?.Value);
  const set = (next: number | null) => onChange({ ...filter, parameters: { ...filter.parameters, Value: next } });

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Slider
          value={value ?? 0}
          onChange={(_, v) => set(v as number)}
          min={0} max={100} step={1}
          sx={{ flex: 1 }}
          aria-label={t.filterPercentage}
        />
        <PercentInput value={value} onChange={set} label="%" />
      </Box>
      <Typography sx={HINT}>{t.percentageHint}</Typography>
    </>
  );
};

/** A number from 0 to 100; empty while typed over. */
const PercentInput = ({ value, onChange, label, width = 120 }: {
  value: number | null;
  onChange: (v: number | null) => void;
  label: string;
  width?: number;
}) => (
  <TextField
    value={value ?? ''}
    onChange={e => onChange(e.target.value === '' ? null : asNumber(e.target.value))}
    label={label}
    size="small"
    type="number"
    slotProps={{ htmlInput: { min: 0, max: 100, step: 1 } }}
    sx={{ width }}
  />
);

// ── Time window ────────────────────────────────────────────────────────────

const TimeWindowEditor = ({ filter, onChange }: EditorProps) => {
  const { t } = useI18n();
  const parameters = filter.parameters ?? {};
  const set = (name: 'Start' | 'End', value: string) => {
    const next: FilterParameters = { ...parameters };
    const time = fromLocalInput(value);
    if (time) next[name] = time;
    else delete next[name];
    onChange({ ...filter, parameters: next });
  };

  const state = windowState(parameters);
  const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  const stateLabel = { active: t.windowActive, upcoming: t.windowUpcoming, ended: t.windowEnded, unknown: null }[state];

  return (
    <>
      <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
        {(['Start', 'End'] as const).map(name => (
          <TextField
            key={name}
            label={name === 'Start' ? t.windowStart : t.windowEnd}
            type="datetime-local"
            size="small"
            value={toLocalInput(asText(parameters[name]))}
            onChange={e => set(name, e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
            sx={{ minWidth: 220 }}
          />
        ))}
        {stateLabel && (
          <Chip
            size="small"
            label={stateLabel}
            color={state === 'active' ? 'success' : 'default'}
            variant={state === 'active' ? 'filled' : 'outlined'}
          />
        )}
      </Box>
      <Typography sx={HINT}>{t.windowTimeZone(zone)}</Typography>
      {parameters.Recurrence != null && <Alert severity="info" sx={{ fontSize: '0.75rem' }}>{t.recurrenceKept}</Alert>}
    </>
  );
};

// ── Targeting ──────────────────────────────────────────────────────────────

const TargetingEditor = ({ filter, onChange }: EditorProps) => {
  const { t } = useI18n();
  const audience = readAudience(filter.parameters);
  const set = (next: Partial<Audience>) =>
    onChange({ ...filter, parameters: { ...filter.parameters, Audience: { ...audience, ...next } } });

  const setGroup = (index: number, patch: Partial<Audience['Groups'][number]>) =>
    set({ Groups: audience.Groups.map((g, i) => (i === index ? { ...g, ...patch } : g)) });

  return (
    <>
      <Typography sx={HINT}>{t.targetingHint}</Typography>
      <ChipsInput label={t.targetUsers} values={audience.Users} onChange={Users => set({ Users })} />

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        <Typography sx={{ ...MONO, fontSize: '0.75rem', fontWeight: 600 }}>{t.targetGroups}</Typography>
        {audience.Groups.map((group, index) => (
          <Box key={index} sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
            <TextField
              label={t.groupName}
              size="small"
              value={group.Name}
              onChange={e => setGroup(index, { Name: e.target.value })}
              sx={{ flex: 1 }}
            />
            <PercentInput
              label={t.groupRollout}
              width={140}
              value={group.RolloutPercentage}
              onChange={RolloutPercentage => setGroup(index, { RolloutPercentage })}
            />
            <Tooltip title={t.removeGroup}>
              <IconButton size="small" onClick={() => set({ Groups: audience.Groups.filter((_, i) => i !== index) })}>
                <DeleteOutlinedIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </Box>
        ))}
        <Box>
          <Button
            size="small"
            startIcon={<AddIcon />}
            onClick={() => set({ Groups: [...audience.Groups, { Name: '', RolloutPercentage: 100 }] })}
            sx={{ ...MONO, fontSize: '0.72rem', textTransform: 'none' }}
          >
            {t.addGroup}
          </Button>
        </Box>
      </Box>

      <PercentInput
        label={t.defaultRollout}
        width={200}
        value={audience.DefaultRolloutPercentage}
        onChange={DefaultRolloutPercentage => set({ DefaultRolloutPercentage })}
      />

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}>
        <ChipsInput
          label={t.excludedUsers}
          values={audience.Exclusion.Users}
          onChange={Users => set({ Exclusion: { ...audience.Exclusion, Users } })}
        />
        <ChipsInput
          label={t.excludedGroups}
          values={audience.Exclusion.Groups}
          onChange={Groups => set({ Exclusion: { ...audience.Exclusion, Groups } })}
        />
      </Box>
    </>
  );
};

// ── Custom filter ──────────────────────────────────────────────────────────

/** Reports whether the JSON typed so far is valid, so that the page can block the save. */
export type JsonValidity = (valid: boolean) => void;

const CustomFilterEditor = ({ filter, onChange, registered, onValidity }: EditorProps & { registered: string[]; onValidity?: JsonValidity }) => {
  const { t } = useI18n();
  const [text, setText] = useState(() => JSON.stringify(filter.parameters ?? {}, null, 2));
  const [invalid, setInvalid] = useState(false);

  // A filter replaced from outside (discard, reload) brings its own parameters.
  useEffect(() => {
    setText(current => {
      try {
        if (JSON.stringify(JSON.parse(current)) === JSON.stringify(filter.parameters ?? {})) return current;
      } catch { /* the text was not valid: replace it */ }
      return JSON.stringify(filter.parameters ?? {}, null, 2);
    });
  }, [filter.parameters]);

  const changeText = (next: string) => {
    setText(next);
    try {
      const parsed = JSON.parse(next === '' ? '{}' : next);
      if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error();
      setInvalid(false);
      onValidity?.(true);
      onChange({ ...filter, parameters: parsed });
    } catch {
      setInvalid(true);
      onValidity?.(false);
    }
  };

  const custom = registered.filter(r => filterKind(r) === 'Custom');
  const unregistered = registered.length > 0 && filter.name.trim() !== '' && !isRegisteredFilter(filter.name, registered);

  return (
    <>
      <Autocomplete
        freeSolo
        options={custom}
        value={filter.name}
        onInputChange={(_, name) => onChange({ ...filter, name })}
        renderInput={params => <TextField {...params} label={t.filterName} size="small" />}
      />
      {unregistered && <Alert severity="warning" sx={{ fontSize: '0.75rem' }}>{t.unregisteredFilter}</Alert>}
      <TextField
        label={t.filterParameters}
        value={text}
        onChange={e => changeText(e.target.value)}
        error={invalid}
        helperText={invalid ? t.invalidJson : undefined}
        multiline
        minRows={3}
        size="small"
        slotProps={{ htmlInput: { spellCheck: false, style: { ...MONO, fontSize: '0.78rem' } } }}
      />
    </>
  );
};
