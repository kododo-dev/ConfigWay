import { useTheme } from '@mui/material/styles';

interface HighlightProps {
  text: string;
  query: string;
}

const Highlight = ({ text, query }: HighlightProps) => {
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';

  if (!query.trim()) return <>{text}</>;

  const escaped = query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const regex = new RegExp(`(${escaped})`, 'gi');
  const parts = text.split(regex);
  
  return (
    <>
      {parts.map((part, i) => {
        const isMatch = part.toLowerCase() === query.toLowerCase();
        return isMatch ? (
          <mark
            key={i}
            style={{
              background: isDark ? '#6b4f00' : '#fff59d',
              color: isDark ? '#ffd54f' : '#4a3500',
              borderRadius: '2px',
              padding: '0 2px',
              fontFamily: 'inherit',
              fontSize: 'inherit',
              fontWeight: 'inherit',
            }}
          >
            {part}
          </mark>
        ) : (
          <span key={i}>{part}</span>
        );
      })}
    </>
  );
};

export default Highlight;
