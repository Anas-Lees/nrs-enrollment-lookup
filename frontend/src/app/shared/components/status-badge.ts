import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';

import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-status-badge',
  imports: [],
  templateUrl: './status-badge.html',
  styleUrl: './status-badge.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadge {
  protected readonly i18n = inject(TranslationService);

  readonly status = input.required<string>();

  /** Localized label, falling back to the raw code for any unmapped status. */
  readonly label = computed(() => {
    const key = 'status.' + this.status();
    const translated = this.i18n.t(key);
    return translated === key ? this.status() : translated;
  });

  readonly cssClass = computed(() => {
    switch (this.status()) {
      case 'ACTIVE':
        return 'badge--ok';
      case 'EXPIRED':
        return 'badge--warn';
      case 'BLOCKED':
      case 'LOST':
      case 'STOLEN':
      case 'CANCELLED':
      case 'REJECTED':
        return 'badge--danger';
      case 'DECEASED':
      case 'MERGED':
      case 'DRAFT':
        return 'badge--muted';
      case 'APPROVED':
        return 'badge--ok';
      case 'UNDER_REVIEW':
        return 'badge--warn';
      default:
        return 'badge--default';
    }
  });
}
