import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Collapse from '@mui/material/Collapse';
import FolderOpenIcon from '@mui/icons-material/FolderOpen';
import FolderIcon from '@mui/icons-material/Folder';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import RestoreIcon from '@mui/icons-material/Restore';
import FieldEditor from './FieldEditor';
import ArrayCard from './ArrayCard';
import type { Section, Field } from '../../api/api.model';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '@mui/material/styles';
import Highlight from '../common/Highlight';
import { fieldFullKey, buildFieldResetPatch, buildSectionResetPatch, sectionHasOverrides } from '../../utils/settings';
import type { ResetPatch } from '../../utils/settings';

export interface SectionCardProps {
  section: Section;
  prefix: string;               // full key of this section
  draft: Record<string, string>;
  onChange: (key: string, value: string) => void;
  onAdd: (patch: Record<string, string>) => void;
  onRemove: (keysToDelete: string[]) => void;
  onReset: (patch: ResetPatch) => void;
  depth?: number;
  searchQuery?: string;
}

const MONO = { fontFamily: "'IBM Plex Mono', monospace" };

function getDepthColors(depth: number, isDark: boolean) {
  if (isDark) {
    const backgrounds = ['#242424', '#1e1e1e', '#191919', '#151515'];
    const borders     = ['#2e2e2e', '#282828', '#222222', '#1c1c1c'];
    const headers     = ['#1e1e1e', '#1a1a1a', '#161616', '#121212'];
    const i = Math.min(depth, backgrounds.length - 1);
    return { background: backgrounds[i], border: borders[i], header: headers[i] };
  } else {
    const backgrounds = ['#ffffff', '#f8f8f8', '#f2f2f2', '#ebebeb'];
    const borders     = ['#e0e0e0', '#d8d8d8', '#cfcfcf', '#c5c5c5'];
    const headers     = ['#f5f5f5', '#efefef', '#e8e8e8', '#e0e0e0'];
    const i = Math.min(depth, backgrounds.length - 1);
    return { background: backgrounds[i], border: borders[i], header: headers[i] };
  }
}

function matches(text: string, q: string) {
  return text.toLowerCase().includes(q.toLowerCase());
}

function fieldMatches(field: Field, fullKey: string, draft: Record<string, string>, q: string): boolean {
  if (!q) return true;
  return (
    matches(field.name, q) ||
    matches(field.key, q) ||
    matches(draft[fullKey] ?? '', q) ||
    matches(field.value ?? '', q)
  );
}

function sectionMatches(section: Section, prefix: string, draft: Record<string, string>, q: string): boolean {
  if (!q) return true;
  if (matches(section.name, q) || matches(section.key, q)) return true;
  if (section.fields.some(f => fieldMatches(f, fieldFullKey(prefix, f.key), draft, q))) return true;
  return section.sections.some(sub => sectionMatches(sub, fieldFullKey(prefix, sub.key), draft, q));
}

interface DescriptionBadgeProps { description: string; isDark: boolean; }

const DescriptionBadge = ({ description, isDark }: DescriptionBadgeProps) => (
  <Tooltip title={description} placement="top" arrow>
    <InfoOutlinedIcon sx={{
      fontSize: 13,
      ml: 0.75,
      color: isDark ? '#444' : '#c0c0c0',
      cursor: 'help',
      flexShrink: 0,
      verticalAlign: 'middle',
      '&:hover': { color: isDark ? '#888' : '#888' },
    }} />
  </Tooltip>
);

interface SubSectionProps {
  section: Section;
  prefix: string;               // full key of this subsection
  draft: Record<string, string>;
  onChange: (key: string, value: string) => void;
  onAdd: (patch: Record<string, string>) => void;
  onRemove: (keysToDelete: string[]) => void;
  onReset: (patch: ResetPatch) => void;
  depth: number;
  searchQuery: string;
}

const SubSection = ({ section, prefix, draft, onChange, onAdd, onRemove, onReset, depth, searchQuery }: SubSectionProps) => {
  const [userCollapsed, setUserCollapsed] = useState(false);
  const theme = useTheme();
  const { t } = useI18n();
  const isDark = theme.palette.mode === 'dark';
  const c = getDepthColors(depth, isDark);

  const isSearching = !!searchQuery;
  const collapsed = isSearching ? false : userCollapsed;

  const hasOverrides = sectionHasOverrides(section, prefix, draft);

  const visibleFields = isSearching
    ? section.fields.filter(f => {
        if (matches(section.name, searchQuery) || matches(section.key, searchQuery)) return true;
        return fieldMatches(f, fieldFullKey(prefix, f.key), draft, searchQuery);
      })
    : section.fields;

  const visibleSubs = isSearching
    ? section.sections.filter(sub => sectionMatches(sub, fieldFullKey(prefix, sub.key), draft, searchQuery))
    : section.sections;

  if (isSearching && visibleFields.length === 0 && visibleSubs.length === 0 && section.arrays.length === 0) return null;

  return (
    <Box sx={{ background: c.background, border: `1px solid ${c.border}`, borderRadius: '6px', overflow: 'hidden' }}>
      <Box
        onClick={() => !isSearching && setUserCollapsed(v => !v)}
        sx={{
          display: 'flex', alignItems: 'center', gap: 1,
          px: 2, py: 1,
          borderBottom: collapsed ? 'none' : `1px solid ${c.border}`,
          background: c.header,
          cursor: isSearching ? 'default' : 'pointer',
          userSelect: 'none',
          '&:hover': isSearching ? {} : { opacity: 0.85 },
        }}
      >
        {collapsed
          ? <FolderIcon sx={{ fontSize: 13, color: isDark ? '#555' : '#aaa' }} />
          : <FolderOpenIcon sx={{ fontSize: 13, color: isDark ? '#555' : '#aaa' }} />
        }
        <Box sx={{ display: 'flex', alignItems: 'center', flex: 1 }}>
          <Typography sx={{ ...MONO, fontSize: '0.74rem', fontWeight: 700, color: theme.palette.text.secondary }}>
            <Highlight text={section.name} query={searchQuery} />
          </Typography>
          {section.description && <DescriptionBadge description={section.description} isDark={isDark} />}
        </Box>
        {hasOverrides && !isSearching && (
          <Tooltip title={t.resetSection} placement="top" arrow>
            <IconButton
              size="small"
              onClick={e => { e.stopPropagation(); onReset(buildSectionResetPatch(section, prefix, draft)); }}
              sx={{ color: isDark ? '#555' : '#ccc', p: '3px', '&:hover': { color: theme.palette.warning.main } }}
            >
              <RestoreIcon sx={{ fontSize: 14 }} />
            </IconButton>
          </Tooltip>
        )}
        {!isSearching && (
          <Tooltip title={collapsed ? t.expand : t.collapse}>
            <Box component="span" sx={{ display: 'flex', color: isDark ? '#444' : '#bbb' }}>
              {collapsed ? <ExpandMoreIcon sx={{ fontSize: 14 }} /> : <ExpandLessIcon sx={{ fontSize: 14 }} />}
            </Box>
          </Tooltip>
        )}
      </Box>
      <Collapse in={!collapsed}>
        <>
          {visibleFields.map(field => {
            const fk = fieldFullKey(prefix, field.key);
            return (
              <FieldEditor
                key={field.key}
                field={field}
                fullKey={fk}
                draft={draft[fk] ?? ''}
                onChange={onChange}
                onReset={() => onReset(buildFieldResetPatch(fk, field.defaultValue))}
                depth={depth}
                searchQuery={searchQuery}
              />
            );
          })}
          {visibleSubs.length > 0 && (
            <Box sx={{ p: 1.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
              {visibleSubs.map(sub => (
                <SubSection
                  key={sub.key}
                  section={sub}
                  prefix={fieldFullKey(prefix, sub.key)}
                  draft={draft}
                  onChange={onChange}
                  onAdd={onAdd}
                  onRemove={onRemove}
                  onReset={onReset}
                  depth={depth + 1}
                  searchQuery={searchQuery}
                />
              ))}
            </Box>
          )}
          {section.arrays.length > 0 && (
            <Box sx={{ p: 1.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
              {section.arrays.map(arr => (
                <ArrayCard
                  key={arr.key}
                  array={arr}
                  arrayPrefix={fieldFullKey(prefix, arr.key)}
                  draft={draft}
                  onChange={onChange}
                  onAdd={onAdd}
                  onRemove={onRemove}
                  onReset={onReset}
                  depth={depth}
                  searchQuery={searchQuery}
                />
              ))}
            </Box>
          )}
        </>
      </Collapse>
    </Box>
  );
};

const SectionCard = ({ section, prefix, draft, onChange, onAdd, onRemove, onReset, depth = 0, searchQuery = '' }: SectionCardProps) => {
  const [userCollapsed, setUserCollapsed] = useState(false);
  const theme = useTheme();
  const { t } = useI18n();
  const isDark = theme.palette.mode === 'dark';
  const c = getDepthColors(depth, isDark);

  const isSearching = !!searchQuery;
  const collapsed = isSearching ? false : userCollapsed;

  const hasOverrides = sectionHasOverrides(section, prefix, draft);

  const visibleFields = isSearching
    ? section.fields.filter(f => {
        if (matches(section.name, searchQuery) || matches(section.key, searchQuery)) return true;
        return fieldMatches(f, fieldFullKey(prefix, f.key), draft, searchQuery);
      })
    : section.fields;

  const visibleSubs = isSearching
    ? section.sections.filter(sub => sectionMatches(sub, fieldFullKey(prefix, sub.key), draft, searchQuery))
    : section.sections;

  if (isSearching && visibleFields.length === 0 && visibleSubs.length === 0 && section.arrays.length === 0) return null;

  return (
    <Box sx={{
      background: c.background,
      border: `1px solid ${c.border}`,
      borderRadius: '8px',
      overflow: 'hidden',
      mb: depth === 0 ? 2 : 0,
    }}>
      <Box sx={{
        display: 'flex', alignItems: 'center', gap: 1,
        px: 2, py: 1.25,
        borderBottom: collapsed ? 'none' : `1px solid ${c.border}`,
        background: c.header,
      }}>
        <Box
          onClick={() => !isSearching && setUserCollapsed(v => !v)}
          sx={{
            display: 'flex', alignItems: 'center', gap: 1,
            flex: 1,
            cursor: isSearching ? 'default' : 'pointer',
            userSelect: 'none',
            '&:hover': isSearching ? {} : { opacity: 0.8 },
          }}
        >
          {collapsed
            ? <FolderIcon sx={{ fontSize: 14, color: isDark ? '#555' : '#aaa' }} />
            : <FolderOpenIcon sx={{ fontSize: 14, color: isDark ? '#555' : '#aaa' }} />
          }
          <Box sx={{ display: 'flex', alignItems: 'center' }}>
            <Typography sx={{ ...MONO, fontSize: '0.82rem', fontWeight: 700, color: theme.palette.text.primary }}>
              <Highlight text={section.name} query={searchQuery} />
            </Typography>
            {section.description && <DescriptionBadge description={section.description} isDark={isDark} />}
          </Box>
        </Box>
        {hasOverrides && !isSearching && (
          <Tooltip title={t.resetSection} placement="top" arrow>
            <IconButton
              size="small"
              onClick={() => onReset(buildSectionResetPatch(section, prefix, draft))}
              sx={{ color: isDark ? '#555' : '#ccc', '&:hover': { color: theme.palette.warning.main } }}
            >
              <RestoreIcon sx={{ fontSize: 16 }} />
            </IconButton>
          </Tooltip>
        )}
        {!isSearching && (
          <Tooltip title={collapsed ? t.expand : t.collapse}>
            <IconButton
              size="small"
              onClick={() => setUserCollapsed(v => !v)}
              sx={{ color: isDark ? '#444' : '#ccc', '&:hover': { color: theme.palette.text.secondary } }}
            >
              {collapsed ? <ExpandMoreIcon sx={{ fontSize: 16 }} /> : <ExpandLessIcon sx={{ fontSize: 16 }} />}
            </IconButton>
          </Tooltip>
        )}
      </Box>

      <Collapse in={!collapsed}>
        <>
          {visibleFields.map(field => {
            const fk = fieldFullKey(prefix, field.key);
            return (
              <FieldEditor
                key={field.key}
                field={field}
                fullKey={fk}
                draft={draft[fk] ?? ''}
                onChange={onChange}
                onReset={() => onReset(buildFieldResetPatch(fk, field.defaultValue))}
                depth={0}
                searchQuery={searchQuery}
              />
            );
          })}
          {visibleSubs.length > 0 && (
            <Box sx={{ p: 1.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
              {visibleSubs.map(sub => (
                <SubSection
                  key={sub.key}
                  section={sub}
                  prefix={fieldFullKey(prefix, sub.key)}
                  draft={draft}
                  onChange={onChange}
                  onAdd={onAdd}
                  onRemove={onRemove}
                  onReset={onReset}
                  depth={1}
                  searchQuery={searchQuery}
                />
              ))}
            </Box>
          )}
          {section.arrays.length > 0 && (
            <Box sx={{ p: 1.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
              {section.arrays.map(arr => (
                <ArrayCard
                  key={arr.key}
                  array={arr}
                  arrayPrefix={fieldFullKey(prefix, arr.key)}
                  draft={draft}
                  onChange={onChange}
                  onAdd={onAdd}
                  onRemove={onRemove}
                  onReset={onReset}
                  depth={0}
                  searchQuery={searchQuery}
                />
              ))}
            </Box>
          )}
        </>
      </Collapse>
    </Box>
  );
};

export default SectionCard;
