import { Pipe, PipeTransform } from '@angular/core';

import { Lang, formatDateTime, formatDisplay } from './date-util';

/**
 * Displays an ISO date as "10 Oct 1999" / "10 أكتوبر 1999" — localized month name,
 * always-Latin digits. Pass the active language so the (pure) pipe re-runs on toggle:
 *   {{ person.dateOfBirth | appDate: i18n.lang() }}
 *
 * Pass a third `true` for a full timestamp (adds the local time-of-day, 24-hour):
 *   {{ enrollment.createdAtUtc | appDate: i18n.lang() : true }}  → "7 Oct 1999, 14:35"
 * Use it for event/action instants (created / decided / queued …); leave it off for plain
 * calendar dates (date of birth, card issue/expiry), which have no meaningful time.
 */
@Pipe({ name: 'appDate' })
export class AppDatePipe implements PipeTransform {
  transform(iso: string | null | undefined, lang: Lang, withTime = false): string {
    return withTime ? formatDateTime(iso, lang) : formatDisplay(iso, lang);
  }
}
