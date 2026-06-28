import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-status-badge',
  imports: [],
  templateUrl: './status-badge.html',
  styleUrl: './status-badge.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadge {
  readonly status = input.required<string>();

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
        return 'badge--danger';
      case 'DECEASED':
      case 'MERGED':
        return 'badge--muted';
      default:
        return 'badge--default';
    }
  });
}
