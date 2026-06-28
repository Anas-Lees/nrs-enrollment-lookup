import { Injectable, signal } from '@angular/core';
import en from '../../../assets/i18n/en.json';
import ar from '../../../assets/i18n/ar.json';

export type Lang = 'en' | 'ar';

const STORAGE_KEY = 'nrs.lang';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly dictionaries: Record<Lang, Record<string, string>> = { en, ar };
  readonly lang = signal<Lang>(TranslationService.readStored());

  constructor() {
    this.apply(this.lang());
  }

  private static readStored(): Lang {
    try {
      const v = localStorage.getItem(STORAGE_KEY);
      return v === 'ar' || v === 'en' ? v : 'en';
    } catch {
      return 'en';
    }
  }

  /** Reactive translate — reads the lang signal so callers re-render on change. */
  t(key: string): string {
    return this.dictionaries[this.lang()][key] ?? key;
  }

  setLang(lang: Lang): void {
    this.lang.set(lang);
    this.apply(lang);
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Ignore storage failures (e.g. private mode); language still applies for the session.
    }
  }

  toggle(): void {
    this.setLang(this.lang() === 'en' ? 'ar' : 'en');
  }

  private apply(lang: Lang): void {
    const el = document.documentElement;
    el.lang = lang;
    el.dir = lang === 'ar' ? 'rtl' : 'ltr';
  }
}
