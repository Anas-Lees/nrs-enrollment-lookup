import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';

import { TranslationService } from '../../core/i18n/translation.service';
import { NotificationService } from '../../core/services/notification.service';
import { AppNotification } from '../../core/models/notification.model';

/**
 * The staff notification bell in the sidebar: an unread badge, and a panel listing what the
 * review workflow has to say — review queued (reviewers), decision made (the submitting
 * operator), SLA escalation (supervisors). Message bodies come bilingual from the API.
 */
@Component({
  selector: 'app-notification-bell',
  imports: [],
  templateUrl: './notification-bell.html',
  styleUrl: './notification-bell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationBell implements OnInit {
  protected readonly i18n = inject(TranslationService);
  protected readonly notifications = inject(NotificationService);

  readonly open = signal(false);

  ngOnInit(): void {
    this.notifications.start();
  }

  toggle(): void {
    this.open.update((v) => !v);
    if (this.open()) {
      this.notifications.refresh();
    }
  }

  close(): void {
    this.open.set(false);
  }

  message(n: AppNotification): string {
    return this.i18n.lang() === 'ar' ? n.messageAr : n.messageEn;
  }

  markRead(n: AppNotification): void {
    if (!n.readAtUtc) {
      this.notifications.markRead(n.id);
    }
  }

  markAll(): void {
    this.notifications.markAllRead();
  }
}
