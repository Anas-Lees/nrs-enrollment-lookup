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
import { SortSelect, SortOption } from '../../shared/components/sort-select';
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
  imports: [FormsModule, NgTemplateOutlet, SortSelect, AppDatePipe],
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

  /** Enrollment id of the task showing the request-corrections note form. */
  readonly correcting = signal<string | null>(null);
  correctionNote = '';

  readonly sortBy = signal('oldest');
  readonly sortOptions: SortOption[] = [
    { value: 'oldest', label: 'sort.oldest' },
    { value: 'newest', label: 'sort.newest' },
    { value: 'name-asc', label: 'sort.nameAsc' },
    { value: 'name-desc', label: 'sort.nameDesc' },
    { value: 'type', label: 'sort.type' },
  ];

  /** Waiting in the shared queue, unassigned — anyone may claim. */
  readonly available = computed(() => this.sortTasks(this.tasks().filter((t) => !t.assignee)));

  /** Claimed by me — mine to decide or release. */
  readonly mine = computed(() =>
    this.sortTasks(this.tasks().filter((t) => t.assignee === this.auth.username())),
  );

  /** In progress with a colleague — shown read-only for transparency. */
  readonly others = computed(() =>
    this.sortTasks(this.tasks().filter((t) => t.assignee && t.assignee !== this.auth.username())),
  );

  /** Client-side sort of an already-loaded group (reacts to the sort selection). */
  private sortTasks(list: ReviewTask[]): ReviewTask[] {
    const name = (t: ReviewTask) =>
      (this.i18n.lang() === 'ar'
        ? `${t.enrollment.familyNameAr} ${t.enrollment.firstNameAr}`
        : `${t.enrollment.familyNameEn} ${t.enrollment.firstNameEn}`
      ).toLowerCase();
    const arr = [...list];
    switch (this.sortBy()) {
      case 'newest':
        return arr.sort((a, b) => b.queuedAtUtc.localeCompare(a.queuedAtUtc));
      case 'name-asc':
        return arr.sort((a, b) => name(a).localeCompare(name(b)));
      case 'name-desc':
        return arr.sort((a, b) => name(b).localeCompare(name(a)));
      case 'type':
        return arr.sort((a, b) => a.enrollment.type.localeCompare(b.enrollment.type));
      default:
        return arr.sort((a, b) => a.queuedAtUtc.localeCompare(b.queuedAtUtc)); // oldest first
    }
  }

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

  /** Background refresh with no loading flicker; paused during an action or an open form. */
  private refresh(): void {
    if (this.busy() || this.rejecting() || this.correcting()) {
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

  /** A high-risk (identity-integrity) review is a supervisor's job. */
  isHighRisk(t: ReviewTask): boolean {
    return t.enrollment.riskLevel === 'HIGH';
  }

  /** Whether the current user is allowed to claim this item (supervisor for high-risk). */
  canClaim(t: ReviewTask): boolean {
    return !this.isHighRisk(t) || this.auth.isSupervisor();
  }

  claim(t: ReviewTask): void {
    if (this.busy() || !this.canClaim(t)) {
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
        if (err.status === 403) {
          // High-risk: supervisor only.
          this.error.set(this.i18n.t('review.supervisorOnly'));
          this.load();
        } else if (err.status === 409 || err.status === 404) {
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
    this.correcting.set(null);
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

  /** First click opens the corrections-note form; submit sends it back to the operator. */
  startCorrections(t: ReviewTask): void {
    this.rejecting.set(null);
    this.correctionNote = '';
    this.correcting.set(t.enrollment.id);
  }

  cancelCorrections(): void {
    this.correcting.set(null);
    this.correctionNote = '';
  }

  submitCorrections(t: ReviewTask): void {
    const note = this.correctionNote.trim();
    if (!note || this.busy()) {
      return;
    }
    this.busy.set(t.enrollment.id);
    this.error.set(null);
    this.enrollments.requestCorrections(t.enrollment.id, note).subscribe({
      next: () => {
        this.busy.set(null);
        this.correcting.set(null);
        this.correctionNote = '';
        // It leaves the reviewer's hands (back to the operator), so drop it from the list.
        this.tasks.update((list) => list.filter((x) => x.enrollment.id !== t.enrollment.id));
        this.notifications.refresh();
        setTimeout(() => this.refresh(), 1000);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(null);
        if (err.status === 409 || err.status === 404 || err.status === 403) {
          this.error.set(this.i18n.t('review.conflict'));
          this.load();
        } else {
          this.error.set(this.i18n.t('review.error'));
        }
      },
    });
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
