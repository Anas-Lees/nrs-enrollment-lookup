import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

import { TranslationService } from '../../core/i18n/translation.service';
import { AuthService } from '../../core/services/auth.service';
import { EnrollmentService } from '../../core/services/enrollment.service';
import { NotificationService } from '../../core/services/notification.service';
import { ReviewTask } from '../../core/models/enrollment.model';
import { AppDatePipe } from '../../shared/app-date.pipe';

/**
 * The reviewer's work queue: open Camunda review tasks, oldest first. A reviewer claims a
 * task, examines the screening flags, and approves — or rejects with a mandatory reason.
 * The decision completes the Camunda user task; the workflow applies the status and
 * notifies the submitting operator.
 */
@Component({
  selector: 'app-review-tasks',
  imports: [FormsModule, AppDatePipe],
  templateUrl: './review-tasks.html',
  styleUrl: './review-tasks.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewTasks implements OnInit {
  protected readonly i18n = inject(TranslationService);
  protected readonly auth = inject(AuthService);
  private readonly enrollments = inject(EnrollmentService);
  private readonly notifications = inject(NotificationService);

  readonly tasks = signal<ReviewTask[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** Enrollment id of the task whose decision is in flight (disables its actions). */
  readonly busy = signal<string | null>(null);

  /** Enrollment id of the task showing the reject-reason form. */
  readonly rejecting = signal<string | null>(null);
  rejectReason = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.enrollments.listReviewTasks().subscribe({
      next: (tasks) => {
        this.tasks.set(tasks);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.i18n.t('review.error'));
        this.loading.set(false);
      },
    });
  }

  applicantName(t: ReviewTask): string {
    const e = t.enrollment;
    return this.i18n.lang() === 'ar'
      ? `${e.firstNameAr} ${e.familyNameAr}`
      : `${e.firstNameEn} ${e.familyNameEn}`;
  }

  /** Claimed by me, unclaimed, or claimed by someone else — drives the action states. */
  claimState(t: ReviewTask): 'mine' | 'unclaimed' | 'other' {
    if (!t.assignee) {
      return 'unclaimed';
    }
    return t.assignee === this.auth.username() ? 'mine' : 'other';
  }

  claim(t: ReviewTask): void {
    if (!t.userTaskKey || this.busy()) {
      return;
    }
    this.busy.set(t.enrollment.id);
    this.enrollments.claimReviewTask(t.userTaskKey).subscribe({
      next: () => {
        this.busy.set(null);
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(null);
        if (err.status === 409 || err.status === 404) {
          // A colleague claimed it first — refresh to show who has it now.
          this.error.set(this.i18n.t('review.claimTaken'));
          this.load();
        } else {
          this.error.set(this.i18n.t('review.error'));
        }
      },
    });
  }

  approve(t: ReviewTask): void {
    if (this.busy()) {
      return;
    }
    const msg = this.i18n.t('review.confirmApprove').replace('{ref}', t.enrollment.referenceNumber);
    if (!window.confirm(msg)) {
      return;
    }
    this.decide(t, true, null);
  }

  /** First click opens the reason form; submit sends the rejection. */
  startReject(t: ReviewTask): void {
    this.rejectReason = '';
    this.rejecting.set(t.enrollment.id);
  }

  cancelReject(): void {
    this.rejecting.set(null);
    this.rejectReason = '';
  }

  submitReject(t: ReviewTask): void {
    const reason = this.rejectReason.trim();
    if (!reason || this.busy()) {
      return;
    }
    this.decide(t, false, reason);
  }

  private decide(t: ReviewTask, approved: boolean, notes: string | null): void {
    this.busy.set(t.enrollment.id);
    this.error.set(null);
    this.enrollments.decide(t.enrollment.id, approved, notes).subscribe({
      next: () => {
        this.busy.set(null);
        this.rejecting.set(null);
        this.rejectReason = '';
        this.load();
        this.notifications.refresh();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(null);
        if (err.status === 409 || err.status === 404) {
          // Decided elsewhere in the meantime — refresh to show reality.
          this.error.set(this.i18n.t('review.conflict'));
          this.load();
        } else {
          this.error.set(this.i18n.t('review.error'));
        }
      },
    });
  }
}
