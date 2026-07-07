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

/**
 * Format a full ISO instant (e.g. "2026-07-07T01:23:45Z") for display with the time-of-day:
 * "7 Oct 1999, 14:35" / "7 أكتوبر 1999، 14:35". Shows the operator's LOCAL time (24-hour),
 * Latin digits, so everyone can see exactly when something happened. A value with no time part
 * falls back to the date-only display.
 */
export function formatDateTime(iso: string | null | undefined, lang: Lang): string {
  if (!iso) {
    return '';
  }
  // No time component (a plain calendar date) → don't invent a time.
  if (!iso.includes('T')) {
    return formatDisplay(iso, lang);
  }
  const dt = new Date(iso);
  if (Number.isNaN(dt.getTime())) {
    return formatDisplay(iso, lang);
  }
  const month = lang === 'ar' ? MONTHS_FULL.ar[dt.getMonth()] : MONTHS_SHORT_EN[dt.getMonth()];
  const time = `${pad2(dt.getHours())}:${pad2(dt.getMinutes())}`;
  const sep = lang === 'ar' ? '،' : ',';
  return `${dt.getDate()} ${month} ${dt.getFullYear()}${sep} ${time}`;
}

function pad2(n: number): string {
  return String(n).padStart(2, '0');
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
