import { Injectable, signal } from '@angular/core';
import en from '../../../assets/i18n/en.json';
import ar from '../../../assets/i18n/ar.json';

export type Lang = 'en' | 'ar';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly dictionaries: Record<Lang, Record<string, string>> = { en, ar };
  readonly lang = signal<Lang>('en');

  constructor() {
    this.apply('en');
  }

  /** Reactive translate — reads the lang signal so callers re-render on change. */
  t(key: string): string {
    return this.dictionaries[this.lang()][key] ?? key;
  }

  setLang(lang: Lang): void {
    this.lang.set(lang);
    this.apply(lang);
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
