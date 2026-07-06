import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { TranslationService } from '../../core/i18n/translation.service';
import { AuthService } from '../../core/services/auth.service';
import { EnrollmentService } from '../../core/services/enrollment.service';
import { NotificationService } from '../../core/services/notification.service';
import { ReviewTask } from '../../core/models/enrollment.model';
import { AppDatePipe } from '../../shared/app-date.pipe';

/**
 * The reviewer's workspace, split the way a real task queue is: applications waiting in the
 * shared queue (claim one to take ownership), the ones assigned to me (which only I can decide
 * or release), and — for transparency — the ones colleagues are handling. Deciding is offered
 * only on my own claimed items; a rejection needs a mandatory reason. "View details" opens the
 * full application read-only. Everything keys off the enrollment id.
 */
@Component({
  selector: 'app-review-tasks',
  imports: [FormsModule, NgTemplateOutlet, AppDatePipe],
  templateUrl: './review-tasks.html',
  styleUrl: './review-tasks.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewTasks implements OnInit {
  protected readonly i18n = inject(TranslationService);
  protected readonly auth = inject(AuthService);
  private readonly enrollments = inject(EnrollmentService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly tasks = signal<ReviewTask[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** Enrollment id of the task whose action (claim/release/decide) is in flight. */
  readonly busy = signal<string | null>(null);

  /** Enrollment id of the task showing the reject-reason form. */
  readonly rejecting = signal<string | null>(null);
  rejectReason = '';

  /** Waiting in the shared queue, unassigned — anyone may claim. */
  readonly available = computed(() => this.tasks().filter((t) => !t.assignee));

  /** Claimed by me — mine to decide or release. */
  readonly mine = computed(() => this.tasks().filter((t) => t.assignee === this.auth.username()));

  /** In progress with a colleague — shown read-only for transparency. */
  readonly others = computed(() =>
    this.tasks().filter((t) => t.assignee && t.assignee !== this.auth.username()),
  );

  ngOnInit(): void {
    this.load();
    // Live queue: silently re-poll so newly-queued applications, and claims/decisions made by
    // other reviewers, appear without a manual refresh.
    const timer = setInterval(() => this.refresh(), 12_000);
    this.destroyRef.onDestroy(() => clearInterval(timer));
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

  /** Background refresh with no loading flicker; paused during an action or the reject form. */
  private refresh(): void {
    if (this.busy() || this.rejecting()) {
      return;
    }
    this.enrollments.listReviewTasks().subscribe({
      next: (tasks) => this.tasks.set(tasks),
      error: () => undefined, // a failed poll just tries again next tick
    });
  }

  applicantName(t: ReviewTask): string {
    const e = t.enrollment;
    return this.i18n.lang() === 'ar'
      ? `${e.firstNameAr} ${e.familyNameAr}`
      : `${e.firstNameEn} ${e.familyNameEn}`;
  }

  viewDetails(t: ReviewTask): void {
    this.router.navigate(['/enrollment', t.enrollment.id]);
  }

  claim(t: ReviewTask): void {
    if (this.busy()) {
      return;
    }
    const id = t.enrollment.id;
    this.busy.set(id);
    this.error.set(null);
    this.enrollments.claimReviewTask(id).subscribe({
      next: () => {
        // Reflect the claim immediately, then reconcile on the next poll.
        const me = this.auth.username();
        this.tasks.update((list) =>
          list.map((x) =>
            x.enrollment.id === id
              ? {
                  ...x,
                  assignee: me,
                  enrollment: { ...x.enrollment, status: 'UNDER_REVIEW', assignedTo: me },
                }
              : x,
          ),
        );
        this.busy.set(null);
        setTimeout(() => this.refresh(), 800);
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

  release(t: ReviewTask): void {
    if (this.busy()) {
      return;
    }
    const msg = this.i18n.t('review.confirmRelease').replace('{ref}', t.enrollment.referenceNumber);
    if (!window.confirm(msg)) {
      return;
    }
    const id = t.enrollment.id;
    this.busy.set(id);
    this.error.set(null);
    this.enrollments.releaseReviewTask(id).subscribe({
      next: () => {
        // Back to the shared queue, unassigned.
        this.tasks.update((list) =>
          list.map((x) =>
            x.enrollment.id === id
              ? {
                  ...x,
                  assignee: null,
                  enrollment: { ...x.enrollment, status: 'PENDING_REVIEW', assignedTo: null },
                }
              : x,
          ),
        );
        this.busy.set(null);
        setTimeout(() => this.refresh(), 800);
      },
      error: () => {
        this.busy.set(null);
        this.error.set(this.i18n.t('review.error'));
        this.load();
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
        // Drop the decided task immediately, then reconcile.
        this.tasks.update((list) => list.filter((x) => x.enrollment.id !== t.enrollment.id));
        this.notifications.refresh();
        setTimeout(() => this.refresh(), 1000);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(null);
        if (err.status === 409 || err.status === 404 || err.status === 403) {
          // Decided elsewhere, or no longer mine — refresh to show reality.
          this.error.set(this.i18n.t('review.conflict'));
          this.load();
        } else {
          this.error.set(this.i18n.t('review.error'));
        }
      },
    });
  }
}
