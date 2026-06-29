import { Pipe, PipeTransform } from '@angular/core';

import { Lang, formatDisplay } from './date-util';

/**
 * Displays an ISO date as "10 Oct 1999" / "10 أكتوبر 1999" — localized month name,
 * always-Latin digits. Pass the active language so the (pure) pipe re-runs on toggle:
 *   {{ person.dateOfBirth | appDate: i18n.lang() }}
 */
@Pipe({ name: 'appDate' })
export class AppDatePipe implements PipeTransform {
  transform(iso: string | null | undefined, lang: Lang): string {
    return formatDisplay(iso, lang);
  }
}
