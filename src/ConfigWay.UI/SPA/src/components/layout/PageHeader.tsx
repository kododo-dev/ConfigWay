import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import InputBase from '@mui/material/InputBase';
import Tooltip from '@mui/material/Tooltip';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import RefreshIcon from '@mui/icons-material/Refresh';
import SaveIcon from '@mui/icons-material/Save';
import SearchIcon from '@mui/icons-material/Search';
import CloseIcon from '@mui/icons-material/Close';
import type { SxProps } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { useI18n } from '../../i18n/I18nContext';

interface PageHeaderProps {
  title: string;
  onRefresh?: () => void;
  hasChanges?: boolean;
  saving?: boolean;
  onSave?: () => void;
  onDiscard?: () => void;
  errors?: string[];
  searchQuery?: string;
  onSearchChange?: (q: string) => void;
  sx?: SxProps;
}

const MONO = { fontFamily: "'IBM Plex Mono', monospace" };

const PageHeader = ({
  title, onRefresh,
  hasChanges, saving, onSave, onDiscard,
  errors,
  searchQuery, onSearchChange,
  sx,
}: PageHeaderProps) => {
  const theme = useTheme();
  const { t } = useI18n();
  const isDark = theme.palette.mode === 'dark';

  const borderColor = isDark ? '#2e2e2e' : '#e0e0e0';
  const headerBg = isDark ? '#1e1e1e' : '#fff';
  const searchBg = isDark ? '#2a2a2a' : '#f5f5f5';
  const searchBorder = isDark ? '#363636' : '#ddd';
  const searchFocusBorder = theme.palette.primary.main;

  const showSearch = !!onSearchChange;
  const showSave = !!onSave;

  return (
    <Box sx={{ flexShrink: 0, ...sx }}>
      <Box sx={{
        display: 'flex',
        alignItems: 'center',
        px: 3,
        py: 1.5,
        borderBottom: `1px solid ${borderColor}`,
        background: headerBg,
        gap: 1.5,
        minHeight: 56,
      }}>
        <Box sx={{ minWidth: 0, flexShrink: 0 }}>
          <Typography sx={{
            ...MONO,
            fontSize: '1rem',
            fontWeight: 700,
            color: theme.palette.text.primary,
            lineHeight: 1.2,
            whiteSpace: 'nowrap',
          }}>
            {title}
          </Typography>
        </Box>
        
        {showSearch && (
          <Box sx={{
            flex: 1,
            mx: 2,
            display: 'flex',
            alignItems: 'center',
            gap: 0.75,
            background: searchBg,
            border: `1px solid ${searchQuery ? searchFocusBorder : searchBorder}`,
            borderRadius: '6px',
            px: 1.25,
            py: 0.5,
            transition: 'border-color 0.15s',
            '&:focus-within': {
              borderColor: searchFocusBorder,
              background: isDark ? '#2e2e2e' : '#fff',
            },
          }}>
            <SearchIcon sx={{ fontSize: 15, color: isDark ? '#555' : '#bbb', flexShrink: 0 }} />
            <InputBase
              value={searchQuery}
              onChange={e => onSearchChange!(e.target.value)}
              placeholder={t.searchPlaceholder}
              fullWidth
              sx={{
                ...MONO,
                fontSize: '0.78rem',
                color: theme.palette.text.primary,
                '& input': { p: 0 },
                '& input::placeholder': { color: isDark ? '#444' : '#bbb', fontStyle: 'italic', opacity: 1 },
              }}
            />
            {searchQuery && (
              <IconButton
                size="small"
                onClick={() => onSearchChange!('')}
                sx={{ p: 0.25, color: isDark ? '#555' : '#bbb', '&:hover': { color: theme.palette.text.primary } }}
              >
                <CloseIcon sx={{ fontSize: 13 }} />
              </IconButton>
            )}
          </Box>
        )}
        
        {!showSearch && <Box sx={{ flex: 1 }} />}
        
        {showSave && hasChanges && onDiscard && (
          <Button
            size="small"
            onClick={onDiscard}
            sx={{
              ...MONO,
              fontSize: '0.72rem',
              textTransform: 'none',
              color: isDark ? '#666' : '#999',
              flexShrink: 0,
              '&:hover': { color: theme.palette.text.primary },
            }}
          >
            {t.discard}
          </Button>
        )}
        
        {showSave && (
          <Button
            size="small"
            variant="outlined"
            startIcon={
              saving
                ? <CircularProgress size={11} sx={{ color: 'inherit' }} />
                : <SaveIcon sx={{ fontSize: '13px !important' }} />
            }
            onClick={onSave}
            disabled={saving || !hasChanges}
            sx={{
              ...MONO,
              fontSize: '0.72rem',
              textTransform: 'none',
              flexShrink: 0,
              borderColor: hasChanges ? theme.palette.primary.main : (isDark ? '#2e2e2e' : '#ddd'),
              color: hasChanges ? theme.palette.primary.main : (isDark ? '#3a3a3a' : '#ccc'),
              '&:hover:not(.Mui-disabled)': {
                borderColor: theme.palette.primary.main,
                background: isDark ? 'rgba(144,202,249,0.06)' : 'rgba(25,118,210,0.06)',
              },
              '&.Mui-disabled': {
                borderColor: isDark ? '#2a2a2a' : '#e0e0e0',
                color: isDark ? '#3a3a3a' : '#ccc',
              },
              transition: 'border-color 0.15s, color 0.15s',
            }}
          >
            {saving ? t.saving : t.save}
          </Button>
        )}
        
        {onRefresh && (
          <Tooltip title={t.refreshTooltip}>
            <IconButton
              size="small"
              onClick={onRefresh}
              sx={{ color: theme.palette.text.secondary, flexShrink: 0, '&:hover': { color: theme.palette.primary.main } }}
            >
              <RefreshIcon sx={{ fontSize: 18 }} />
            </IconButton>
          </Tooltip>
        )}
      </Box>
      
      {errors && errors.length > 0 && (
        <Box sx={{
          px: 3,
          background: isDark ? '#1e1e1e' : '#fff',
          borderBottom: `1px solid ${isDark ? '#5a2a2a' : '#ffcdd2'}`,
        }}>
          <Alert
            severity="error"
            sx={{
              background: isDark ? '#2a1a1a' : '#fff5f5',
              border: 'none',
              borderRadius: 0,
              color: isDark ? '#f48fb1' : '#c62828',
              fontSize: '0.75rem',
              fontFamily: 'monospace',
              px: 0,
              '& .MuiAlert-icon': { color: '#f44336' },
            }}
          >
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.2 }}>
              {errors.map((err, i) => <span key={i}>{err}</span>)}
            </Box>
          </Alert>
        </Box>
      )}
    </Box>
  );
};

export default PageHeader;
