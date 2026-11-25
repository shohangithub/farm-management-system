import { Injectable } from '@angular/core';

/** The two languages the interface can be shown in. */
export type AppLanguage = 'en' | 'bn';

const STORAGE_KEY = 'farm360.language';

/** Text values a translated string can carry, written `{name}` inside it. */
export type TranslationParams = Record<string, string | number | null | undefined>;

/** Reads the saved choice; English when nothing is saved or storage is blocked. */
export function readStoredLanguage(): AppLanguage {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'bn' ? 'bn' : 'en';
  } catch {
    return 'en';
  }
}

/** Templates carry line breaks and indentation; keys and lookups both ignore them. */
export function normalizeKey(text: string): string {
  return text.replace(/\s+/g, ' ').trim();
}

let dictionary: ReadonlyMap<string, string> = new Map();

/**
 * Loads the Bangla dictionary before the app starts (called from an app initializer), so
 * English users never download it. The language is fixed for the life of the page: switching
 * saves the choice and reloads, which also re-renders every date and number in the new locale.
 */
export async function loadTranslations(language: AppLanguage): Promise<void> {
  document.documentElement.lang = language;
  document.documentElement.classList.toggle('lang-bn', language === 'bn');
  if (language !== 'bn') return;

  const { BN } = await import('./bn');
  const map = new Map<string, string>();
  for (const [key, value] of Object.entries(BN)) {
    if (value) map.set(normalizeKey(key), value);
  }
  dictionary = map;
}

/**
 * Translates interface text. The key is the English text itself, so anything not yet in the
 * dictionary simply shows in English. `{name}` placeholders are filled from `params` after
 * translation, so word order can change between languages:
 * `t('Showing {from} to {to} of {total} animals', { from: 1, to: 20, total: 84 })`.
 */
export function translate(text: string | null | undefined, params?: TranslationParams): string {
  if (text === null || text === undefined) return '';
  const source = String(text);
  const translated = dictionary.get(normalizeKey(source)) ?? source;
  if (!params) return translated;
  return translated.replace(/\{(\w+)\}/g, (match, name: string) => {
    const value = params[name];
    return value === null || value === undefined ? match : String(value);
  });
}

@Injectable({ providedIn: 'root' })
export class I18nService {
  /** The language this page was loaded in. */
  readonly language: AppLanguage = readStoredLanguage();
  readonly isBangla = this.language === 'bn';

  /** Translates text for use in TypeScript: snack bars, dialogs, confirm messages, chart labels. */
  t(text: string | null | undefined, params?: TranslationParams): string {
    return translate(text, params);
  }

  /** Saves the choice and reloads, so every screen, date and number switches at once. */
  setLanguage(language: AppLanguage): void {
    if (language === this.language) return;
    try {
      localStorage.setItem(STORAGE_KEY, language);
    } catch {
      // Storage blocked: the switch still applies to this load.
    }
    window.location.reload();
  }

  toggle(): void {
    this.setLanguage(this.isBangla ? 'en' : 'bn');
  }
}
