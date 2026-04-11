import React, { createContext, useContext, useState, useCallback, useMemo } from 'react';
import { createTheme, type Theme } from '@mui/material/styles';

type ThemeMode = 'dark' | 'light';

interface ThemeContextValue {
  mode: ThemeMode;
  toggleMode: () => void;
  muiTheme: Theme;
}

const STORAGE_KEY = 'configwayF-theme';

function buildTheme(mode: ThemeMode): Theme {
  const isDark = mode === 'dark';
  return createTheme({
    palette: {
      mode,
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
    typography: {
      fontFamily: "'IBM Plex Mono', 'Fira Code', monospace",
    },
    components: {
      MuiDrawer: {
        styleOverrides: {
          paper: {
            background: isDark ? '#222' : '#fafafa',
            borderRight: `1px solid ${isDark ? '#333' : '#e0e0e0'}`,
          },
        },
      },
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
          input: {
            fontFamily: "'IBM Plex Mono', monospace",
            fontSize: '0.875rem',
          },
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
        styleOverrides: {
          root: {
            fontFamily: "'IBM Plex Mono', monospace",
            fontSize: '0.7rem',
          },
        },
      },
    },
  });
}

const ThemeContext = createContext<ThemeContextValue>({
  mode: 'dark',
  toggleMode: () => {},
  muiTheme: buildTheme('dark'),
});

export const ThemeContextProvider = ({ children }: { children: React.ReactNode }) => {
  const [mode, setMode] = useState<ThemeMode>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === 'light' ? 'light' : 'dark';
  });

  const toggleMode = useCallback(() => {
    setMode(prev => {
      const next = prev === 'dark' ? 'light' : 'dark';
      localStorage.setItem(STORAGE_KEY, next);
      return next;
    });
  }, []);

  const muiTheme = useMemo(() => buildTheme(mode), [mode]);

  return (
    <ThemeContext.Provider value={{ mode, toggleMode, muiTheme }}>
      {children}
    </ThemeContext.Provider>
  );
};

export const useThemeMode = () => useContext(ThemeContext);
