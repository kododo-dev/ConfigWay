import { useI18n as useShellI18n } from '@kododo-dev/way-shell';
import { translations, type Language, type TranslationKeys } from './translations';

/** ConfigWay's strings in the language the shell shows (the user's choice, the host's, then the browser's). */
export const useI18n = () => {
  const { language } = useShellI18n();
  const lang: Language = language in translations ? (language as Language) : 'en';
  return { lang, t: translations[lang] as TranslationKeys };
};
