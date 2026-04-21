import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Collapse from '@mui/material/Collapse';
import Button from '@mui/material/Button';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import RestoreIcon from '@mui/icons-material/Restore';
import FolderOpenIcon from '@mui/icons-material/FolderOpen';
import FolderIcon from '@mui/icons-material/Folder';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { useTheme } from '@mui/material/styles';
import { useI18n } from '../../i18n/I18nContext';
import FieldEditor from './FieldEditor';
import type { ArrayField, ArrayItem, Field } from '../../api/api.model';
import {
  itemFullKey, itemFieldFullKey,
  buildNewItemDraft, collectItemKeys, nextArrayIndex,
  buildArrayResetPatch, arrayHasOverrides,
  buildFieldResetPatch, complexItemHasOverrides, buildComplexItemResetPatch,
} from '../../utils/settings';
import type { ResetPatch } from '../../utils/settings';
import Highlight from '../common/Highlight';

const MONO = { fontFamily: "'IBM Plex Mono', monospace" };

interface ArrayCardProps {
  array: ArrayField;
  arrayPrefix: string;         // full key of the array (parent prefix + ":" + array.key)
  draft: Record<string, string>;
  onChange: (key: string, value: string) => void;
  onAdd: (patch: Record<string, string>) => void;
  onRemove: (keysToDelete: string[]) => void;
  onReset: (patch: ResetPatch) => void;
  depth?: number;
  searchQuery?: string;
}

const ArrayCard = ({
  array, arrayPrefix, draft, onChange, onAdd, onRemove, onReset, depth = 0, searchQuery = '',
}: ArrayCardProps) => {
  const theme  = useTheme();
  const { t }  = useI18n();
  const isDark = theme.palette.mode === 'dark';
  const [collapsed, setCollapsed] = useState(false);

  const borderColor  = isDark ? '#2e2e2e' : '#ddd';
  const headerBg     = isDark ? '#1e1e1e' : '#f5f5f5';
  const bg           = isDark ? '#242424' : '#fff';

  // Server items: always show non-deletable ones; show deletable ones only if their key is still in draft
  const serverItemIndices = array.items
    .filter(item => {
      if (!item.isDeletable) return true;
      if (array.isSimple) return itemFullKey(arrayPrefix, item.index) in draft;
      return collectItemKeys(array, arrayPrefix, item.index).some(k => k in draft);
    })
    .map(i => i.index);

  // Draft-only items: locally added, not yet saved (not in array.items)
  const serverIndexSet = new Set(array.items.map(i => i.index));
  const draftOnlyIndices = Object.keys(draft)
    .filter(k => k.startsWith(arrayPrefix + ':'))
    .map(k => parseInt(k.slice(arrayPrefix.length + 1).split(':')[0], 10))
    .filter(n => !isNaN(n) && !serverIndexSet.has(n));

  const allIndices: number[] = [...new Set([...serverItemIndices, ...draftOnlyIndices])].sort((a, b) => a - b);

  const hasOverrides = arrayHasOverrides(array, arrayPrefix, draft);

  const handleAdd = () => {
    const newIndex = nextArrayIndex({ ...array, items: allIndices.map(i => ({ index: i } as ArrayItem)) });
    onAdd(buildNewItemDraft(array, arrayPrefix, newIndex));
  };

  const handleRemove = (index: number) => {
    const originalItem = array.items.find(i => i.index === index);
    const serverKeys = originalItem
      ? collectItemKeys(array, arrayPrefix, index)
      : [];
    const draftPrefix = itemFullKey(arrayPrefix, index) + ':';
    const simpleKey   = itemFullKey(arrayPrefix, index);
    const draftKeys   = Object.keys(draft).filter(
      k => k === simpleKey || k.startsWith(draftPrefix)
    );
    onRemove(serverKeys);
    draftKeys.forEach(k => onChange(k, '__DELETE__'));
  };

  const handleReset = () => {
    onReset(buildArrayResetPatch(array, arrayPrefix, draft));
  };

  const isSearching = !!searchQuery;

  return (
    <Box sx={{
      border: `1px solid ${borderColor}`,
      borderRadius: '6px',
      overflow: 'hidden',
      background: bg,
    }}>
      {/* Header */}
      <Box
        sx={{
          display: 'flex', alignItems: 'center', gap: 1,
          px: 2, py: 1,
          borderBottom: collapsed ? 'none' : `1px solid ${borderColor}`,
          background: headerBg,
          cursor: 'pointer',
          userSelect: 'none',
          '&:hover': { opacity: 0.85 },
        }}
        onClick={() => setCollapsed(v => !v)}
      >
        {collapsed
          ? <FolderIcon sx={{ fontSize: 13, color: isDark ? '#555' : '#aaa' }} />
          : <FolderOpenIcon sx={{ fontSize: 13, color: isDark ? '#555' : '#aaa' }} />
        }
        <Box sx={{ display: 'flex', alignItems: 'center', flex: 1, gap: 0.5 }}>
          <Typography sx={{ ...MONO, fontSize: '0.74rem', fontWeight: 700, color: theme.palette.text.secondary }}>
            <Highlight text={array.name} query={searchQuery} />
          </Typography>
          <Typography sx={{ ...MONO, fontSize: '0.7rem', color: isDark ? '#555' : '#bbb', ml: 0.5 }}>
            [{allIndices.length}]
          </Typography>
          {array.description && (
            <Tooltip title={array.description} placement="top" arrow>
              <InfoOutlinedIcon sx={{
                fontSize: 13, ml: 0.25,
                color: isDark ? '#444' : '#ccc',
                cursor: 'help',
                '&:hover': { color: theme.palette.text.secondary },
              }} />
            </Tooltip>
          )}
        </Box>
        {hasOverrides && (
          <Tooltip title={t.resetArray} placement="top" arrow>
            <IconButton
              size="small"
              onClick={e => { e.stopPropagation(); handleReset(); }}
              sx={{ color: isDark ? '#555' : '#ccc', p: '3px', '&:hover': { color: theme.palette.warning.main } }}
            >
              <RestoreIcon sx={{ fontSize: 14 }} />
            </IconButton>
          </Tooltip>
        )}
        <Tooltip title={collapsed ? t.expand : t.collapse}>
          <Box component="span" sx={{ display: 'flex', color: isDark ? '#444' : '#bbb' }}>
            {collapsed ? <ExpandMoreIcon sx={{ fontSize: 14 }} /> : <ExpandLessIcon sx={{ fontSize: 14 }} />}
          </Box>
        </Tooltip>
      </Box>

      <Collapse in={!collapsed}>
        <Box>
          {allIndices.map(index => {
            const serverItem = array.items.find(i => i.index === index);
            const isDeletable = serverItem ? serverItem.isDeletable : true;
            const itemHasOvr  = !isDeletable && complexItemHasOverrides(array, arrayPrefix, index, draft);

            const simpleItemField: Field | null = (!array.isSimple || !serverItem) ? null : {
              key:          String(index),
              name:         '',
              type:         (serverItem.type ?? 'String') as Field['type'],
              value:        serverItem.value,
              defaultValue: serverItem.defaultValue,
              isSensitive:  false,
              hasOverride:  false,
              description:  null,
              options:      serverItem.options ?? null,
            };

            return (
              <Box
                key={index}
                sx={{
                  borderBottom: `1px solid ${borderColor}`,
                  '&:last-child': { borderBottom: 'none' },
                }}
              >
                {/* Item header row */}
                <Box sx={{
                  display: 'flex', alignItems: 'center',
                  px: 1.5, py: 0.5,
                  background: isDark ? '#1c1c1c' : '#fafafa',
                  borderBottom: `1px solid ${isDark ? '#222' : '#eee'}`,
                }}>
                  <Typography sx={{ ...MONO, fontSize: '0.7rem', color: isDark ? '#555' : '#bbb', flex: 1 }}>
                    [{index}]
                  </Typography>
                  {!isDeletable && itemHasOvr && (
                    <Tooltip title={t.resetItem} placement="top" arrow>
                      <IconButton
                        size="small"
                        onClick={() => onReset(buildComplexItemResetPatch(array, arrayPrefix, index, draft))}
                        sx={{ color: isDark ? '#555' : '#ccc', p: '3px', '&:hover': { color: theme.palette.warning.main } }}
                      >
                        <RestoreIcon sx={{ fontSize: 14 }} />
                      </IconButton>
                    </Tooltip>
                  )}
                  {isDeletable && (
                    <Tooltip title="Remove">
                      <IconButton
                        size="small"
                        onClick={() => handleRemove(index)}
                        sx={{ color: isDark ? '#555' : '#ccc', '&:hover': { color: theme.palette.error.main } }}
                      >
                        <DeleteOutlineIcon sx={{ fontSize: 14 }} />
                      </IconButton>
                    </Tooltip>
                  )}
                </Box>

                {/* Item content */}
                {array.isSimple ? (
                  <SimpleItemEditor
                    index={index}
                    arrayPrefix={arrayPrefix}
                    draft={draft}
                    onChange={onChange}
                    item={serverItem ?? array.template}
                    onReset={!isDeletable && simpleItemField
                      ? () => onReset(buildFieldResetPatch(itemFullKey(arrayPrefix, index), simpleItemField))
                      : undefined}
                    depth={depth}
                    searchQuery={searchQuery}
                  />
                ) : (
                  <ComplexItemContent
                    index={index}
                    arrayPrefix={arrayPrefix}
                    item={serverItem ?? array.template}
                    isDeletable={isDeletable}
                    draft={draft}
                    onChange={onChange}
                    onReset={onReset}
                    depth={depth}
                    searchQuery={searchQuery}
                    isSearching={isSearching}
                  />
                )}
              </Box>
            );
          })}

          {/* Add button */}
          <Box sx={{ px: 1.5, py: 1, display: 'flex', justifyContent: 'flex-start' }}>
            <Button
              size="small"
              startIcon={<AddIcon sx={{ fontSize: 13 }} />}
              onClick={handleAdd}
              sx={{
                ...MONO,
                fontSize: '0.72rem',
                color: isDark ? '#666' : '#aaa',
                textTransform: 'none',
                '&:hover': { color: theme.palette.primary.main },
              }}
            >
              {t.addItem}
            </Button>
          </Box>
        </Box>
      </Collapse>
    </Box>
  );
};

// ── Simple item editor ────────────────────────────────────────────────────────

interface SimpleItemEditorProps {
  index: number;
  arrayPrefix: string;
  item: ArrayItem;
  draft: Record<string, string>;
  onChange: (key: string, value: string) => void;
  onReset?: () => void;
  depth: number;
  searchQuery: string;
}

const SimpleItemEditor = ({ index, arrayPrefix, item, draft, onChange, onReset, depth, searchQuery }: SimpleItemEditorProps) => {
  const fullKey = itemFullKey(arrayPrefix, index);
  const syntheticField: Field = {
    key:          String(index),
    name:         '',
    type:         (item.type ?? 'String') as Field['type'],
    value:        item.value,
    defaultValue: item.defaultValue,
    isSensitive:  false,
    hasOverride:  false,
    description:  null,
    options:      item.options ?? null,
  };

  return (
    <FieldEditor
      field={syntheticField}
      fullKey={fullKey}
      draft={draft[fullKey] ?? ''}
      onChange={onChange}
      onReset={onReset}
      depth={depth + 1}
      searchQuery={searchQuery}
    />
  );
};

// ── Complex item content ──────────────────────────────────────────────────────

interface ComplexItemContentProps {
  index: number;
  arrayPrefix: string;
  item: ArrayItem;
  isDeletable: boolean;
  draft: Record<string, string>;
  onChange: (key: string, value: string) => void;
  onReset: (patch: ResetPatch) => void;
  depth: number;
  searchQuery: string;
  isSearching: boolean;
}

const ComplexItemContent = ({
  index, arrayPrefix, item, isDeletable, draft, onChange, onReset, depth, searchQuery,
}: ComplexItemContentProps) => {
  const theme  = useTheme();
  const isDark = theme.palette.mode === 'dark';
  const itemPrefix = itemFullKey(arrayPrefix, index);

  return (
    <Box sx={{ pl: 0 }}>
      {item.fields.map(f => {
        const fk = itemFieldFullKey(arrayPrefix, index, f.key);
        return (
          <FieldEditor
            key={f.key}
            field={f}
            fullKey={fk}
            draft={draft[fk] ?? ''}
            onChange={onChange}
            onReset={!isDeletable ? () => onReset(buildFieldResetPatch(fk, f)) : undefined}
            depth={depth + 1}
            searchQuery={searchQuery}
          />
        );
      })}
      {item.sections.length > 0 && (
        <Box sx={{ p: 1, display: 'flex', flexDirection: 'column', gap: 1 }}>
          {item.sections.map(sub => {
            const subPrefix = `${itemPrefix}:${sub.key}`;
            return (
              <NestedSectionInItem
                key={sub.key}
                section={sub}
                prefix={subPrefix}
                draft={draft}
                onChange={onChange}
                onReset={!isDeletable ? onReset : undefined}
                depth={depth + 1}
                searchQuery={searchQuery}
                isDark={isDark}
              />
            );
          })}
        </Box>
      )}
      {item.arrays.map(nestedArr => (
        <Box key={nestedArr.key} sx={{ p: 1 }}>
          <ArrayCard
            array={nestedArr}
            arrayPrefix={`${itemPrefix}:${nestedArr.key}`}
            draft={draft}
            onChange={onChange}
            onAdd={() => {}}
            onRemove={() => {}}
            onReset={() => {}}
            depth={depth + 1}
            searchQuery={searchQuery}
          />
        </Box>
      ))}
    </Box>
  );
};

// Minimal nested section renderer for inside array items
interface NestedSectionInItemProps {
  section: { key: string; name: string; description: string | null; fields: import('../../api/api.model').Field[]; sections: import('../../api/api.model').Section[]; arrays: import('../../api/api.model').ArrayField[] };
  prefix: string;
  draft: Record<string, string>;
  onChange: (key: string, value: string) => void;
  onReset?: (patch: ResetPatch) => void;
  depth: number;
  searchQuery: string;
  isDark: boolean;
}

const NestedSectionInItem = ({ section, prefix, draft, onChange, onReset, depth, searchQuery, isDark }: NestedSectionInItemProps) => {
  const theme = useTheme();
  const border = isDark ? '#2a2a2a' : '#eee';
  return (
    <Box sx={{ border: `1px solid ${border}`, borderRadius: '4px', overflow: 'hidden' }}>
      <Box sx={{ px: 1.5, py: 0.5, background: isDark ? '#1c1c1c' : '#f8f8f8', borderBottom: `1px solid ${border}` }}>
        <Typography sx={{ fontFamily: "'IBM Plex Mono', monospace", fontSize: '0.72rem', fontWeight: 700, color: theme.palette.text.secondary }}>
          {section.name}
        </Typography>
      </Box>
      {section.fields.map(f => {
        const fk = `${prefix}:${f.key}`;
        return (
          <FieldEditor
            key={f.key}
            field={f}
            fullKey={fk}
            draft={draft[fk] ?? ''}
            onChange={onChange}
            onReset={onReset ? () => onReset(buildFieldResetPatch(fk, f)) : undefined}
            depth={depth}
            searchQuery={searchQuery}
          />
        );
      })}
      {section.sections.map(sub => (
        <NestedSectionInItem
          key={sub.key}
          section={sub}
          prefix={`${prefix}:${sub.key}`}
          draft={draft}
          onChange={onChange}
          onReset={onReset}
          depth={depth + 1}
          searchQuery={searchQuery}
          isDark={isDark}
        />
      ))}
    </Box>
  );
};

export default ArrayCard;
