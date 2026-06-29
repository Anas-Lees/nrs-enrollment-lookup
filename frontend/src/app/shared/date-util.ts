/**
 * Small date helpers for the custom (bilingual) date picker and date display.
 * Month/weekday NAMES follow the language; DIGITS are always Latin (0-9).
 */
export type Lang = 'en' | 'ar';

export const MONTHS_FULL: Record<Lang, string[]> = {
  en: [
    'January',
    'February',
    'March',
    'April',
    'May',
    'June',
    'July',
    'August',
    'September',
    'October',
    'November',
    'December',
  ],
  ar: [
    'يناير',
    'فبراير',
    'مارس',
    'أبريل',
    'مايو',
    'يونيو',
    'يوليو',
    'أغسطس',
    'سبتمبر',
    'أكتوبر',
    'نوفمبر',
    'ديسمبر',
  ],
};

const MONTHS_SHORT_EN = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

export const WEEKDAYS_SHORT: Record<Lang, string[]> = {
  en: ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'],
  ar: ['أحد', 'إثن', 'ثلا', 'أرب', 'خمي', 'جمع', 'سبت'],
};

/** Format an ISO date (yyyy-mm-dd) for display: "10 Oct 1999" / "10 أكتوبر 1999" (Latin digits). */
export function formatDisplay(iso: string | null | undefined, lang: Lang): string {
  const p = parseIso(iso);
  if (!p) {
    return '';
  }
  const month = lang === 'ar' ? MONTHS_FULL.ar[p.m - 1] : MONTHS_SHORT_EN[p.m - 1];
  return `${p.d} ${month} ${p.y}`;
}

export function parseIso(
  iso: string | null | undefined,
): { y: number; m: number; d: number } | null {
  if (!iso) {
    return null;
  }
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  if (!match) {
    return null;
  }
  return { y: Number(match[1]), m: Number(match[2]), d: Number(match[3]) };
}

export function toIso(y: number, m: number, d: number): string {
  const mm = String(m).padStart(2, '0');
  const dd = String(d).padStart(2, '0');
  return `${y}-${mm}-${dd}`;
}

export function daysInMonth(y: number, m: number): number {
  return new Date(y, m, 0).getDate();
}

/** Day of week (0=Sunday) for the 1st of the given month. */
export function firstWeekday(y: number, m: number): number {
  return new Date(y, m - 1, 1).getDay();
}
