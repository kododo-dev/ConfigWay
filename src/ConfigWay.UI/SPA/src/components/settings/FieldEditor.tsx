import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import InputBase from '@mui/material/InputBase';
import Switch from '@mui/material/Switch';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import RestoreIcon from '@mui/icons-material/Restore';
import type { Field } from '../../api/api.model';
import { SENSITIVE_RESET } from '../../utils/settings';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '@mui/material/styles';
import Highlight from '../common/Highlight';

interface FieldEditorProps {
  field: Field;
  fullKey: string;
  draft: string;
  onChange: (key: string, value: string) => void;
  onReset?: () => void;
  depth?: number;
  searchQuery?: string;
}

const MONO = { fontFamily: "'IBM Plex Mono', monospace" };

const FieldEditor = ({ field, fullKey, draft, onChange, onReset, depth = 0, searchQuery = '' }: FieldEditorProps) => {
  const { t } = useI18n();
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';

  const borderColor      = isDark ? '#2e2e2e' : '#ddd';
  const borderHoverColor = isDark ? '#444'    : '#bbb';
  const borderFocusColor = theme.palette.primary.main;
  const inputBg          = isDark ? '#141414' : '#ffffff';

  const isOverridden = onReset != null && (
    field.isSensitive
      ? draft !== SENSITIVE_RESET && (draft !== '' || field.value !== null)
      : draft !== (field.defaultValue ?? '')
  );

  const renderInput = () => {
    switch (field.type) {
      case 'Bool':
        return (
          <Box sx={{ display: 'flex', alignItems: 'center', height: 36 }}>
            <Switch
              checked={draft === 'True'}
              onChange={e => onChange(fullKey, e.target.checked ? 'True' : 'False')}
              size="small"
            />
            <Typography sx={{ ...MONO, fontSize: '0.78rem', color: theme.palette.text.secondary, ml: 0.5 }}>
              {draft === 'True' ? 'True' : 'False'}
            </Typography>
          </Box>
        );

      case 'Number':
        return (
          <InputBase
            value={draft}
            onChange={e => onChange(fullKey, e.target.value)}
            placeholder={field.defaultValue || t.notSet}
            inputProps={{ inputMode: 'numeric', pattern: '[0-9]*[.,]?[0-9]*' }}
            fullWidth
            sx={{
              ...MONO,
              fontSize: '0.78rem',
              color: theme.palette.text.primary,
              background: inputBg,
              border: `1px solid ${borderColor}`,
              borderRadius: '4px',
              px: 1,
              py: '6px',
              lineHeight: 1.5,
              transition: 'border-color 0.15s',
              '&:hover': { borderColor: borderHoverColor },
              '&.Mui-focused': { borderColor: borderFocusColor },
              '& input': { ...MONO, fontSize: '0.78rem', p: 0 },
              '& input::placeholder': {
                color: isDark ? '#444' : '#bbb',
                fontStyle: 'italic',
                opacity: 1,
              },
            }}
          />
        );

      case 'Enum':
        return (
          <Select
            value={draft}
            onChange={e => onChange(fullKey, e.target.value)}
            displayEmpty
            size="small"
            fullWidth
            sx={{
              ...MONO,
              fontSize: '0.78rem',
              background: inputBg,
              '& .MuiOutlinedInput-notchedOutline': { borderColor },
              '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: borderHoverColor },
              '&.Mui-focused .MuiOutlinedInput-notchedOutline': { borderColor: borderFocusColor },
              '& .MuiSelect-select': { ...MONO, fontSize: '0.78rem', py: '6px' },
            }}
          >
            {(field.options ?? []).map(opt => (
              <MenuItem key={opt.value} value={opt.value} sx={{ ...MONO, fontSize: '0.78rem' }}>
                {opt.label}
              </MenuItem>
            ))}
          </Select>
        );

      default:
        if (field.isSensitive) {
          return (
            <InputBase
              value={draft === SENSITIVE_RESET ? '' : draft}
              onChange={e => onChange(fullKey, e.target.value)}
              placeholder={draft === SENSITIVE_RESET ? t.notSet : (field.value !== null ? '●●●●●●●●' : t.notSet)}
              type="password"
              fullWidth
              sx={{
                ...MONO,
                fontSize: '0.78rem',
                color: theme.palette.text.primary,
                background: inputBg,
                border: `1px solid ${borderColor}`,
                borderRadius: '4px',
                px: 1,
                py: '6px',
                lineHeight: 1.5,
                transition: 'border-color 0.15s',
                '&:hover': { borderColor: borderHoverColor },
                '&.Mui-focused': { borderColor: borderFocusColor },
                '& input': { ...MONO, fontSize: '0.78rem', p: 0 },
                '& input::placeholder': {
                  color: isDark ? '#444' : '#bbb',
                  fontStyle: 'italic',
                  opacity: 1,
                },
              }}
            />
          );
        }
        return (
          <InputBase
            value={draft}
            onChange={e => onChange(fullKey, e.target.value)}
            placeholder={field.defaultValue || t.notSet}
            fullWidth
            multiline
            minRows={1}
            sx={{
              ...MONO,
              fontSize: '0.78rem',
              color: theme.palette.text.primary,
              alignItems: 'flex-start',
              background: inputBg,
              border: `1px solid ${borderColor}`,
              borderRadius: '4px',
              px: 1,
              py: '6px',
              lineHeight: 1.5,
              transition: 'border-color 0.15s',
              '&:hover': { borderColor: borderHoverColor },
              '&.Mui-focused': { borderColor: borderFocusColor },
              '& textarea': {
                ...MONO,
                fontSize: '0.78rem',
                color: theme.palette.text.primary,
                lineHeight: 1.5,
                resize: 'none',
                p: 0,
              },
              '& textarea::placeholder': {
                color: isDark ? '#444' : '#bbb',
                fontStyle: 'italic',
                opacity: 1,
              },
            }}
          />
        );
    }
  };

  return (
    <Box sx={{
      display: 'flex',
      alignItems: 'flex-start',
      gap: 2,
      pl: 2 + depth * 2,
      pr: 1.5,
      py: 0.75,
      borderBottom: `1px solid ${isDark ? '#1e1e1e' : '#f0f0f0'}`,
      '&:last-child': { borderBottom: 'none' },
      minHeight: 44,
    }}>
      {/* Field label */}
      <Box sx={{ width: 180, flexShrink: 0, pt: '7px', display: 'flex', alignItems: 'flex-start', gap: 0.5 }}>
        <Typography sx={{
          ...MONO,
          fontSize: '0.78rem',
          color: theme.palette.text.secondary,
          lineHeight: 1.4,
          flex: 1,
        }}>
          <Highlight text={field.name} query={searchQuery} />
        </Typography>

        {field.description && (
          <Tooltip title={field.description} placement="top" arrow>
            <InfoOutlinedIcon sx={{
              fontSize: 13,
              color: isDark ? '#444' : '#ccc',
              cursor: 'help',
              mt: '1px',
              flexShrink: 0,
              '&:hover': { color: theme.palette.text.secondary },
            }} />
          </Tooltip>
        )}
      </Box>

      {/* Field input */}
      <Box sx={{ flex: 1, display: 'flex', alignItems: 'center' }}>
        {renderInput()}
      </Box>

      {/* Reset button */}
      <Box sx={{ display: 'flex', alignItems: 'center', height: 36, flexShrink: 0 }}>
        {isOverridden && (
          <Tooltip title={t.resetField} placement="top" arrow>
            <IconButton
              size="small"
              onClick={onReset}
              sx={{
                color: isDark ? '#555' : '#ccc',
                p: '3px',
                '&:hover': { color: theme.palette.warning.main },
              }}
            >
              <RestoreIcon sx={{ fontSize: 14 }} />
            </IconButton>
          </Tooltip>
        )}
      </Box>
    </Box>
  );
};

export default FieldEditor;
