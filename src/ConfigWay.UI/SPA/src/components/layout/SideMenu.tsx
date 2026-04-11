import { useLocation, useNavigate } from 'react-router-dom';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import TuneIcon from '@mui/icons-material/Tune';
import FolderOpenIcon from '@mui/icons-material/FolderOpen';
import { useSections } from '../../context/SectionsContext';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '@mui/material/styles';

const SideMenu = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const { sections } = useSections();
  const { t } = useI18n();
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';
  const path = location.pathname;

  const isAll = path === '/' || path === '/all';
  const activeSectionKey = path.startsWith('/section/')
    ? decodeURIComponent(path.split('/section/')[1])
    : null;

  const iconSx       = { color: isDark ? '#666' : '#aaa', minWidth: 32, '& svg': { fontSize: 16 } };
  const activeIconSx = { ...iconSx, color: theme.palette.primary.main };

  const textSx = (active: boolean) => ({
    '& .MuiListItemText-primary': {
      fontSize: '0.8rem',
      fontFamily: "'IBM Plex Mono', monospace",
      color: active ? theme.palette.text.primary : theme.palette.text.secondary,
      fontWeight: active ? 600 : 400,
    },
  });

  const borderColor = isDark ? '#2e2e2e' : '#e0e0e0';

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, overflow: 'hidden' }}>
      <Box sx={{ flexShrink: 0, py: 1 }}>
        <List dense disablePadding>
          <ListItem disablePadding>
            <ListItemButton selected={isAll} onClick={() => navigate('/')}>
              <ListItemIcon sx={isAll ? activeIconSx : iconSx}><TuneIcon /></ListItemIcon>
              <ListItemText primary={t.allSettings} sx={textSx(isAll)} />
            </ListItemButton>
          </ListItem>
        </List>
      </Box>
      
      {sections.length > 0 && (
        <Box sx={{
          display: 'flex',
          flexDirection: 'column',
          flex: 1,
          minHeight: 0,
          overflow: 'hidden',
        }}>
          <Divider sx={{ borderColor, mx: 2, flexShrink: 0 }} />
          
          <Typography sx={{
            px: 2.5, pt: 1, pb: 0.5,
            fontSize: '0.62rem',
            color: isDark ? '#444' : '#bbb',
            textTransform: 'uppercase',
            letterSpacing: '0.1em',
            fontFamily: 'monospace',
            flexShrink: 0,
          }}>
            {t.sections}
          </Typography>
          
          <Box sx={{ flex: 1, minHeight: 0, overflowY: 'auto', pb: 1 }}>
            <List dense disablePadding>
              {sections.map(s => {
                const active = activeSectionKey === s.key;
                return (
                  <ListItem disablePadding key={s.key}>
                    <ListItemButton
                      selected={active}
                      onClick={() => navigate(`/section/${encodeURIComponent(s.key)}`)}
                    >
                      <ListItemIcon sx={active ? activeIconSx : iconSx}><FolderOpenIcon /></ListItemIcon>
                      <ListItemText primary={s.name} sx={textSx(active)} />
                    </ListItemButton>
                  </ListItem>
                );
              })}
            </List>
          </Box>
        </Box>
      )}
    </Box>
  );
};

export default SideMenu;
