import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

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
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly router = inject(Router);

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

  /** Close when a click lands anywhere outside the bell (including the toggle's own host). */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }

  /** Escape closes the panel — expected for any dismissible popover. */
  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.close();
    }
  }

  message(n: AppNotification): string {
    return this.i18n.lang() === 'ar' ? n.messageAr : n.messageEn;
  }

  /**
   * Opening a notification takes you to the enrollment it is about (to review or just view it)
   * and marks it read on the way. A notification with no enrollment (should not happen today)
   * simply gets marked read.
   */
  openEnrollment(n: AppNotification): void {
    if (!n.readAtUtc) {
      this.notifications.markRead(n.id);
    }
    this.close();
    if (n.enrollmentId) {
      this.router.navigate(['/enrollment', n.enrollmentId]);
    }
  }

  markAll(): void {
    this.notifications.markAllRead();
  }
}
