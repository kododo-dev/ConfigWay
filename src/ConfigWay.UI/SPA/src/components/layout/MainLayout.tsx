import React from 'react';
import Drawer from '@mui/material/Drawer';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import DarkModeIcon from '@mui/icons-material/DarkMode';
import LightModeIcon from '@mui/icons-material/LightMode';
import SideMenu from './SideMenu';
import { useThemeMode } from '../../context/ThemeContext';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '@mui/material/styles';

const DRAWER_WIDTH = 248;

const MainLayout = ({ children }: { children: React.ReactNode }) => {
  const { mode, toggleMode } = useThemeMode();
  const { t } = useI18n();
  const theme = useTheme();
  const isDark = mode === 'dark';

  const drawerBg   = isDark ? '#222'    : '#fafafa';
  const borderColor = isDark ? '#2e2e2e' : '#e0e0e0';
  const bg          = isDark ? '#1a1a1a' : '#f5f5f5';

  return (
    <Box sx={{ display: 'flex', height: '100vh', overflow: 'hidden', background: bg }}>
      <Drawer
        variant="permanent"
        sx={{
          width: DRAWER_WIDTH,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: {
            width: DRAWER_WIDTH,
            boxSizing: 'border-box',
            background: drawerBg,
            borderRight: `1px solid ${borderColor}`,
            color: theme.palette.text.primary,
            overflowX: 'hidden',
            display: 'flex',
            flexDirection: 'column',
            height: '100%',
            overflow: 'hidden',
          },
        }}
      >
        <Box sx={{
          px: 2.5, py: 2,
          borderBottom: `1px solid ${borderColor}`,
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          flexShrink: 0,
        }}>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography sx={{
              fontSize: '0.9rem', fontWeight: 700,
              color: theme.palette.text.primary,
              letterSpacing: '0.05em', lineHeight: 1.2,
              fontFamily: "'IBM Plex Mono', monospace",
            }}>
              {t.appTitle}
            </Typography>
          </Box>
          <Tooltip title={isDark ? t.switchToLight : t.switchToDark}>
            <IconButton
              size="small"
              onClick={toggleMode}
              sx={{ color: theme.palette.text.secondary, flexShrink: 0, '&:hover': { color: theme.palette.primary.main } }}
            >
              {isDark
                ? <LightModeIcon sx={{ fontSize: 16 }} />
                : <DarkModeIcon  sx={{ fontSize: 16 }} />
              }
            </IconButton>
          </Tooltip>
        </Box>
        <SideMenu />
      </Drawer>
        
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          minWidth: 0,
          display: 'flex',
          flexDirection: 'column',
          height: '100vh',
          overflow: 'hidden',
          background: bg,
        }}
      >
        {children}
      </Box>
    </Box>
  );
};

export default MainLayout;
