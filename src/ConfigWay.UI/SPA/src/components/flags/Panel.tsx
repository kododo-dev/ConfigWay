import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { useTheme } from '@mui/material/styles';

export const MONO = { fontFamily: "'IBM Plex Mono', monospace" };

interface PanelProps {
  title: ReactNode;
  /** Actions on the right of the title bar. */
  actions?: ReactNode;
  children: ReactNode;
  /** A red border, e.g. for a condition the API turned down. */
  invalid?: boolean;
}

/** A card of the flag editor, in the look of the settings' section cards. */
const Panel = ({ title, actions, children, invalid }: PanelProps) => {
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';
  const border = invalid ? theme.palette.error.main : isDark ? '#2e2e2e' : '#e0e0e0';

  return (
    <Box sx={{
      background: isDark ? '#242424' : '#ffffff',
      border: `1px solid ${border}`,
      borderRadius: '6px',
      overflow: 'hidden',
    }}>
      <Box sx={{
        display: 'flex', alignItems: 'center', gap: 1,
        px: 2, py: 1,
        minHeight: 40,
        borderBottom: `1px solid ${isDark ? '#2e2e2e' : '#e0e0e0'}`,
        background: isDark ? '#1e1e1e' : '#f5f5f5',
      }}>
        <Typography component="div" sx={{ ...MONO, fontSize: '0.8rem', fontWeight: 600, flex: 1, minWidth: 0 }}>
          {title}
        </Typography>
        {actions}
      </Box>
      <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 2 }}>
        {children}
      </Box>
    </Box>
  );
};

export default Panel;
