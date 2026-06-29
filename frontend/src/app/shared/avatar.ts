// Deterministic initials + colour for a person's avatar, shared by the result
// cards, the quick-preview panel and the profile header.
import type { Lang } from '../core/i18n/translation.service';

const AVATAR_PALETTE = ['#1c6b41', '#0b6e75', '#8a5a2b', '#5b4b8a', '#2f5a40', '#7a3b52'];

/**
 * Two-letter initials in the active script: Arabic first/family letters in Arabic mode,
 * English otherwise. Falls back to the other script so an avatar never renders blank.
 * (Arabic is caseless, so toUpperCase only affects the Latin branch.)
 */
export function personInitials(
  firstEn: string,
  familyEn: string,
  firstAr: string,
  familyAr: string,
  lang: Lang,
): string {
  if (lang === 'ar') {
    const a = (firstAr || firstEn)?.charAt(0) ?? '';
    const b = (familyAr || familyEn)?.charAt(0) ?? '';
    return `${a}${b}`;
  }
  const a = (firstEn || firstAr)?.charAt(0) ?? '';
  const b = (familyEn || familyAr)?.charAt(0) ?? '';
  return `${a}${b}`.toUpperCase();
}

/** Stable colour derived from a seed (use the CRN) so an avatar never changes. */
export function avatarColor(seed: string): string {
  const hash = [...(seed ?? '')].reduce((sum, ch) => sum + ch.charCodeAt(0), 0);
  return AVATAR_PALETTE[hash % AVATAR_PALETTE.length];
}
