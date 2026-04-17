export interface Field {
  key: string;
  name: string;
  type: 'String';
  value: string | null;
  description: string | null;
}

export interface Section {
  key: string;
  name: string;
  sections: Section[];
  fields: Field[];
  description: string | null;
}

export interface ValidationResult {
  errors: string[];
}

export interface Setting {
  key: string;
  value: string | null;
}