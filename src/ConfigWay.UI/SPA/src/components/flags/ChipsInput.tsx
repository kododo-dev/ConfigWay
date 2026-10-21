import Autocomplete from '@mui/material/Autocomplete';
import Chip from '@mui/material/Chip';
import TextField from '@mui/material/TextField';
import { useI18n } from '../../i18n/I18nContext';

interface ChipsInputProps {
  label: string;
  values: string[];
  onChange: (values: string[]) => void;
}

/** A list of names typed one by one, e.g. the users of a targeting filter. */
const ChipsInput = ({ label, values, onChange }: ChipsInputProps) => {
  const { t } = useI18n();
  return (
    <Autocomplete
      multiple
      freeSolo
      options={[]}
      value={values}
      onChange={(_, next) => onChange(
        [...new Set(next.map(v => (typeof v === 'string' ? v : String(v)).trim()).filter(v => v !== ''))])}
      renderValue={(items, getItemProps) =>
        items.map((item, index) => {
          const { key, ...props } = getItemProps({ index });
          return <Chip key={key} label={item} size="small" {...props} />;
        })}
      renderInput={params => (
        <TextField {...params} label={label} placeholder={values.length === 0 ? t.chipsHint : undefined} size="small" />
      )}
    />
  );
};

export default ChipsInput;
