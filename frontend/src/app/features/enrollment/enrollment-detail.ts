import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';

import { TranslationService } from '../../core/i18n/translation.service';
import { EnrollmentService } from '../../core/services/enrollment.service';
import { NotificationService } from '../../core/services/notification.service';
import { Enrollment } from '../../core/models/enrollment.model';
import { StatusBadge } from '../../shared/components/status-badge';
import { AppDatePipe } from '../../shared/app-date.pipe';
import { navigateBack } from '../../shared/navigate-back';

/**
 * Read-only view of a single enrollment application: the full applicant record, why screening
 * flagged it, who is handling it, and — once decided — the decision, who made it, and the
 * reason (always shown for a rejection, so the operator can tell the applicant). Also the
 * operator's action surface for the two lifecycle steps they own: resubmitting an application a
 * reviewer sent back for corrections, and withdrawing one before it is decided. Reachable from
 * the operator's queue (click a row) and from a reviewer's task ("View details").
 */
@Component({
  selector: 'app-enrollment-detail',
  imports: [RouterLink, StatusBadge, AppDatePipe],
  templateUrl: './enrollment-detail.html',
  styleUrl: './enrollment-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EnrollmentDetail {
  protected readonly i18n = inject(TranslationService);
  private readonly enrollments = inject(EnrollmentService);
  private readonly notifications = inject(NotificationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  readonly enrollment = signal<Enrollment | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  readonly isRejected = computed(() => this.enrollment()?.status === 'REJECTED');
  readonly isWithdrawn = computed(() => this.enrollment()?.status === 'WITHDRAWN');
  readonly needsCorrection = computed(() => this.enrollment()?.status === 'NEEDS_CORRECTION');
  readonly isDecided = computed(() => {
    const s = this.enrollment()?.status;
    return s === 'APPROVED' || s === 'REJECTED' || s === 'WITHDRAWN';
  });

  /** An application can still be edited until it has been decided (incl. while awaiting corrections). */
  readonly canEdit = computed(() => {
    const s = this.enrollment()?.status;
    return s === 'DRAFT' || s === 'SUBMITTED' || s === 'PENDING_REVIEW' || s === 'NEEDS_CORRECTION';
  });

  /** It can be withdrawn any time before it is concluded. */
  readonly canWithdraw = computed(() => {
    const s = this.enrollment()?.status;
    return (
      s === 'SUBMITTED' ||
      s === 'PENDING_REVIEW' ||
      s === 'UNDER_REVIEW' ||
      s === 'NEEDS_CORRECTION'
    );
  });

  constructor() {
    // React to the id param, not just the first snapshot: opening another enrollment from the
    // notification bell reuses this component (same route), so we must reload on each change.
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      const id = pm.get('id');
      if (id) {
        this.load(id);
      }
    });
  }

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.enrollments.get(id).subscribe({
      next: (e) => {
        this.enrollment.set(e);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.i18n.t('detail.error'));
        this.loading.set(false);
      },
    });
  }

  applicantName(e: Enrollment): string {
    return this.i18n.lang() === 'ar'
      ? `${e.firstNameAr} ${e.familyNameAr}`
      : `${e.firstNameEn} ${e.familyNameEn}`;
  }

  /** Back to wherever the operator came from (queue, review tasks, a notification…). */
  goBack(): void {
    navigateBack(this.location, this.router, '/enrollment/queue');
  }

  flagLabel(flag: string): string {
    const key = 'flag.' + flag;
    const translated = this.i18n.t(key);
    return translated === key ? flag : translated;
  }

  /** Pick the language-appropriate value, falling back to the other script or a dash. */
  pick(en: string | null, ar: string | null): string {
    const v = this.i18n.lang() === 'ar' ? (ar ?? en) : (en ?? ar);
    return v && v.length > 0 ? v : '—';
  }

  /** Does the enrollment carry any residential address? */
  hasAddress(e: Enrollment): boolean {
    return !!(
      e.governorate ||
      e.wilayat ||
      e.village ||
      e.street ||
      e.buildingNumber ||
      e.postalCode
    );
  }

  /** Does the enrollment carry any contact detail? */
  hasContact(e: Enrollment): boolean {
    return !!(e.mobile || e.email);
  }

  resubmit(): void {
    const e = this.enrollment();
    if (!e || this.busy()) {
      return;
    }
    if (
      !window.confirm(this.i18n.t('detail.confirmResubmit').replace('{ref}', e.referenceNumber))
    ) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.enrollments.resubmit(e.id).subscribe({
      next: (updated) => {
        this.enrollment.set(updated);
        this.busy.set(false);
        this.notifications.refresh();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(
          err.status === 409 ? this.i18n.t('detail.resubmitConflict') : this.i18n.t('detail.error'),
        );
        this.load(e.id);
      },
    });
  }

  withdraw(): void {
    const e = this.enrollment();
    if (!e || this.busy()) {
      return;
    }
    if (
      !window.confirm(this.i18n.t('detail.confirmWithdraw').replace('{ref}', e.referenceNumber))
    ) {
      return;
    }
    // Optional reason — Cancel on the prompt still proceeds (reason stays empty).
    const reason = window.prompt(this.i18n.t('detail.withdrawReasonPrompt')) ?? '';
    this.busy.set(true);
    this.error.set(null);
    this.enrollments.withdraw(e.id, reason.trim() || null).subscribe({
      next: (updated) => {
        this.enrollment.set(updated);
        this.busy.set(false);
        this.notifications.refresh();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(
          err.status === 409 ? this.i18n.t('detail.withdrawConflict') : this.i18n.t('detail.error'),
        );
        this.load(e.id);
      },
    });
  }
}
