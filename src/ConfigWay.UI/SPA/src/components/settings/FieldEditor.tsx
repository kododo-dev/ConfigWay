import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import InputBase from '@mui/material/InputBase';
import type { Field } from '../../api/api.model';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '@mui/material/styles';
import Highlight from '../common/Highlight';

interface FieldEditorProps {
  field: Field;
  draft: string;
  onChange: (key: string, value: string) => void;
  depth?: number;
  searchQuery?: string;
}

const MONO = { fontFamily: "'IBM Plex Mono', monospace" };

const FieldEditor = ({ field, draft, onChange, depth = 0, searchQuery = '' }: FieldEditorProps) => {
  const { t } = useI18n();
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';

  const borderColor        = isDark ? '#2e2e2e' : '#ddd';
  const borderHoverColor   = isDark ? '#444'    : '#bbb';
  const borderFocusColor   = theme.palette.primary.main;
  const inputBg            = isDark ? '#141414' : '#ffffff';

  return (
    <Box sx={{
      display: 'flex',
      alignItems: 'flex-start',   // top-align so label stays at top when input grows
      gap: 2,
      pl: 2 + depth * 2,
      pr: 1.5,
      py: 0.75,
      borderBottom: `1px solid ${isDark ? '#1e1e1e' : '#f0f0f0'}`,
      '&:last-child': { borderBottom: 'none' },
      minHeight: 44,
    }}>
      {/* Field label — vertically centred for single-line, top-aligned for multiline */}
      <Typography sx={{
        ...MONO,
        fontSize: '0.78rem',
        color: theme.palette.text.secondary,
        width: 180,
        flexShrink: 0,
        // Nudge label to align with first line of input (py: 0.75 + inner padding ~6px)
        pt: '7px',
        lineHeight: 1.4,
      }}>
        <Highlight text={field.name} query={searchQuery} />
      </Typography>

      {/* Auto-growing textarea via InputBase multiline */}
      <InputBase
        value={draft}
        onChange={e => onChange(field.key, e.target.value)}
        placeholder={field.value ?? t.notSet}
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
          // Textarea inherits font from InputBase sx — override explicitly
          '& textarea': {
            ...MONO,
            fontSize: '0.78rem',
            color: theme.palette.text.primary,
            lineHeight: 1.5,
            resize: 'none',       // let minRows/auto-grow handle sizing
            p: 0,
          },
          '& textarea::placeholder': {
            color: isDark ? '#444' : '#bbb',
            fontStyle: 'italic',
            opacity: 1,
          },
        }}
      />
    </Box>
  );
};

export default FieldEditor;
