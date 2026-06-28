// Deterministic initials + colour for a person's avatar, shared by the result
// cards, the quick-preview panel and the profile header.

const AVATAR_PALETTE = ['#1c6b41', '#0b6e75', '#8a5a2b', '#5b4b8a', '#2f5a40', '#7a3b52'];

export function personInitials(firstEn: string, familyEn: string): string {
  const a = firstEn?.charAt(0) ?? '';
  const b = familyEn?.charAt(0) ?? '';
  return `${a}${b}`.toUpperCase();
}

/** Stable colour derived from a seed (use the CRN) so an avatar never changes. */
export function avatarColor(seed: string): string {
  const hash = [...(seed ?? '')].reduce((sum, ch) => sum + ch.charCodeAt(0), 0);
  return AVATAR_PALETTE[hash % AVATAR_PALETTE.length];
}
