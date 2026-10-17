import type { ThemeOptions } from '@mui/material';

const MONO = "'IBM Plex Mono', 'Fira Code', monospace";

/** ConfigWay's look on top of the shell's theme: monospace type and its own greys, cards and inputs. */
export function configWayTheme(mode: 'light' | 'dark'): ThemeOptions {
  const isDark = mode === 'dark';
  return {
    palette: {
      background: {
        default: isDark ? '#1a1a1a' : '#f5f5f5',
        paper: isDark ? '#242424' : '#ffffff',
      },
      primary: { main: isDark ? '#90caf9' : '#1976d2' },
      secondary: { main: isDark ? '#ce93d8' : '#9c27b0' },
      error: { main: '#f44336' },
      success: { main: '#4caf50' },
      divider: isDark ? '#333' : '#ddd',
      text: {
        primary: isDark ? '#e0e0e0' : '#1a1a1a',
        secondary: isDark ? '#888' : '#555',
      },
    },
    typography: { fontFamily: MONO },
    components: {
      MuiCard: {
        styleOverrides: {
          root: {
            background: isDark ? '#2a2a2a' : '#fff',
            border: `1px solid ${isDark ? '#333' : '#ddd'}`,
          },
        },
      },
      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            '& fieldset': { borderColor: isDark ? '#444' : '#ccc' },
            '&:hover fieldset': { borderColor: isDark ? '#666' : '#999' },
          },
          input: { fontFamily: MONO, fontSize: '0.875rem' },
        },
      },
      MuiListItemButton: {
        styleOverrides: {
          root: {
            borderRadius: 6,
            margin: '2px 8px',
            width: 'calc(100% - 16px)',
            '&.Mui-selected': {
              background: isDark ? '#333' : '#e3f2fd',
              '&:hover': { background: isDark ? '#3a3a3a' : '#bbdefb' },
            },
          },
        },
      },
      MuiChip: {
        styleOverrides: { root: { fontFamily: MONO, fontSize: '0.7rem' } },
      },
    },
  };
}
