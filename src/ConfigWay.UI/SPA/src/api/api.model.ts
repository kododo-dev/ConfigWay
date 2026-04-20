export interface Field {
  key: string;   // relative key (property name)
  name: string;
  type: 'String' | 'Bool' | 'Number' | 'Enum';
  value: string | null;
  defaultValue: string | null;
  description: string | null;
  options: { value: string; label: string }[] | null;
}

export interface Section {
  key: string;   // relative key
  name: string;
  sections: Section[];
  fields: Field[];
  arrays: ArrayField[];
  description: string | null;
}

export interface ArrayField {
  key: string;   // relative key
  name: string;
  description: string | null;
  isSimple: boolean;
  items: ArrayItem[];
  template: ArrayItem;
}

export interface ArrayItem {
  index: number;        // -1 for template
  isDeletable: boolean;
  // simple arrays:
  value: string | null;
  defaultValue: string | null;
  type: 'String' | 'Bool' | 'Number' | 'Enum' | null;
  options: { value: string; label: string }[] | null;
  // complex arrays:
  fields: Field[];
  sections: Section[];
  arrays: ArrayField[];
}

export interface ValidationResult {
  errors: string[];
}

export interface Setting {
  key: string;
  value: string | null;
}
