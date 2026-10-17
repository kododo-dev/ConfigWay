import { useParams } from 'react-router-dom';
import { WayMenu, useShell } from '@kododo-dev/way-shell';
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

// ConfigWay's items in the shell's side menu. Paths are relative to the Way's root: in the panel it is under
// #/config/, on its own page under #/.
const SideMenu = () => {
  const { navigate, wayId } = useShell();
  const { sections } = useSections();
  const { t } = useI18n();
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';
  const path = useParams()['*'] ?? '';

  const isAll = path === '' || path === 'all';
  const activeSectionKey = path.startsWith('section/')
    ? decodeURIComponent(path.slice('section/'.length))
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
    <WayMenu>
      <Box sx={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, overflow: 'hidden' }}>
        <Box sx={{ flexShrink: 0, py: 1 }}>
          <List dense disablePadding>
            <ListItem disablePadding>
              <ListItemButton selected={isAll} onClick={() => navigate(wayId, '')}>
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
                        onClick={() => navigate(wayId, `section/${encodeURIComponent(s.key)}`)}
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
    </WayMenu>
  );
};

export default SideMenu;
