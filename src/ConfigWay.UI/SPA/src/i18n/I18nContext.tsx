import React, { createContext, useContext, useMemo } from 'react';
import { translations, type Language, type TranslationKeys } from './translations';

interface I18nContextValue {
  lang: Language;
  t: TranslationKeys;
}

const I18nContext = createContext<I18nContextValue>({
  lang: 'en',
  t: translations['en'],
});

function detectLanguage(): Language {
  const langs = navigator.languages ?? [navigator.language];
  for (const l of langs) {
    if (l.startsWith('pl')) return 'pl';
    if (l.startsWith('en')) return 'en';
  }
  return 'en';
}

export const I18nProvider = ({ children }: { children: React.ReactNode }) => {
  const lang = useMemo(detectLanguage, []);
  return (
    <I18nContext.Provider value={{ lang, t: translations[lang] as TranslationKeys }}>
      {children}
    </I18nContext.Provider>
  );
};

export const useI18n = () => useContext(I18nContext);
