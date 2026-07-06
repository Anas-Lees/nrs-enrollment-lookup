import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';
import { EnrollmentService } from '../../core/services/enrollment.service';
import { Enrollment } from '../../core/models/enrollment.model';
import { StatusBadge } from '../../shared/components/status-badge';
import { AppDatePipe } from '../../shared/app-date.pipe';

/**
 * Read-only view of a single enrollment application: the full applicant record, why screening
 * flagged it, who is handling it, and — once decided — the decision, who made it, and the
 * reason (always shown for a rejection, so the operator can tell the applicant). Reachable
 * from the operator's queue (click a row) and from a reviewer's task ("View details"). Editing
 * is a separate action, offered only while the application has not been decided.
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
  private readonly route = inject(ActivatedRoute);

  readonly enrollment = signal<Enrollment | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly isRejected = computed(() => this.enrollment()?.status === 'REJECTED');
  readonly isDecided = computed(() => {
    const s = this.enrollment()?.status;
    return s === 'APPROVED' || s === 'REJECTED';
  });

  /** An application can still be edited until it has been decided. */
  readonly canEdit = computed(() => {
    const s = this.enrollment()?.status;
    return s === 'DRAFT' || s === 'SUBMITTED' || s === 'PENDING_REVIEW';
  });

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.load(id);
    }
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

  flagLabel(flag: string): string {
    const key = 'flag.' + flag;
    const translated = this.i18n.t(key);
    return translated === key ? flag : translated;
  }
}
