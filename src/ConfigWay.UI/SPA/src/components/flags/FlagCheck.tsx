import { useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useShell } from '@kododo-dev/way-shell';
import { ApiError } from '../../api/api';
import { evaluateFlag } from '../../api/flags';
import type { FlagEvaluation } from '../../api/flags';
import { useI18n } from '../../i18n/I18nContext';
import Panel, { MONO } from './Panel';

interface FlagCheckProps {
  flagId: string;
  /** The editor has unsaved changes, which the check does not see. */
  hasChanges: boolean;
}

/** Asks the application's feature manager whether the saved flag is on for a user and groups. */
const FlagCheck = ({ flagId, hasChanges }: FlagCheckProps) => {
  const { api } = useShell();
  const { t } = useI18n();
  const [user, setUser] = useState('');
  const [groups, setGroups] = useState('');
  const [result, setResult] = useState<FlagEvaluation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [running, setRunning] = useState(false);

  // A result is about the user and groups it was asked for; editing them clears it.
  const edit = (set: (value: string) => void) => (value: string) => {
    set(value);
    setResult(null);
    setError(null);
  };

  const run = async () => {
    setRunning(true);
    setResult(null);
    setError(null);
    try {
      setResult(await evaluateFlag(
        api,
        flagId,
        user.trim() || null,
        groups.split(',').map(g => g.trim()).filter(g => g !== ''),
      ));
    } catch (e) {
      setResult(null);
      setError(e instanceof ApiError ? e.messages.join(' ') : e instanceof Error ? e.message : t.unexpectedError);
    } finally {
      setRunning(false);
    }
  };

  return (
    <Panel title={t.checkTitle}>
      <Typography sx={{ ...MONO, fontSize: '0.72rem', color: 'text.secondary' }}>{t.checkSavedOnly}</Typography>
      <Box
        component="form"
        onSubmit={e => { e.preventDefault(); run(); }}
        sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}
      >
        <TextField label={t.checkUser} size="small" value={user} onChange={e => edit(setUser)(e.target.value)} />
        <TextField
          label={t.checkGroups}
          size="small"
          value={groups}
          onChange={e => edit(setGroups)(e.target.value)}
          sx={{ flex: 1, minWidth: 200 }}
        />
        <Button
          type="submit"
          variant="outlined"
          size="small"
          disabled={running || hasChanges}
          startIcon={running ? <CircularProgress size={11} sx={{ color: 'inherit' }} /> : undefined}
          sx={{ ...MONO, fontSize: '0.72rem', textTransform: 'none' }}
        >
          {t.checkRun}
        </Button>
      </Box>
      {result && (
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }} data-testid="flag-check-result">
          <Chip
            size="small"
            color={result.enabled ? 'success' : 'default'}
            variant={result.enabled ? 'filled' : 'outlined'}
            label={result.enabled ? t.checkOn : t.checkOff}
          />
          {result.variant && <Chip size="small" variant="outlined" label={t.checkVariant(result.variant)} />}
        </Box>
      )}
      {error && <Alert severity="error" sx={{ fontSize: '0.75rem' }}>{error}</Alert>}
    </Panel>
  );
};

export default FlagCheck;
