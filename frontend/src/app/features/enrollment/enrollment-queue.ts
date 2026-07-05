import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';
import { EnrollmentService } from '../../core/services/enrollment.service';
import { PagedResult } from '../../core/models/paged-result.model';
import { EnrollmentStatus, EnrollmentSummary } from '../../core/models/enrollment.model';
import { Pagination } from '../../shared/components/pagination';
import { StatusBadge } from '../../shared/components/status-badge';
import { AppDatePipe } from '../../shared/app-date.pipe';

/**
 * The operator's enrollment queue: a paged, status-filterable list of applications, newest
 * first. Clicking a row opens it in the edit form. URL-driven (page / size / status) so the
 * view is shareable and bookmarkable, mirroring the search page.
 */
@Component({
  selector: 'app-enrollment-queue',
  imports: [RouterLink, Pagination, StatusBadge, AppDatePipe],
  templateUrl: './enrollment-queue.html',
  styleUrl: './enrollment-queue.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EnrollmentQueue {
  protected readonly i18n = inject(TranslationService);
  private readonly enrollments = inject(EnrollmentService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly results = signal<PagedResult<EnrollmentSummary> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly pageSize = signal(10);
  readonly pageSizeOptions = [10, 25, 50, 100];
  readonly statusFilter = signal<EnrollmentStatus | ''>('');

  readonly statuses: EnrollmentStatus[] = [
    'SUBMITTED',
    'UNDER_REVIEW',
    'APPROVED',
    'REJECTED',
    'DRAFT',
  ];

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      const size = Number(pm.get('size'));
      this.pageSize.set(this.pageSizeOptions.includes(size) ? size : 10);
      this.statusFilter.set((pm.get('status') as EnrollmentStatus | null) ?? '');
      this.load(Number(pm.get('page')) || 1);
    });
  }

  load(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.enrollments
      .list({ status: this.statusFilter() || null, page, pageSize: this.pageSize() })
      .subscribe({
        next: (r) => {
          this.results.set(r);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(this.i18n.t('queue.error'));
          this.loading.set(false);
        },
      });
  }

  onPageChange(page: number): void {
    this.navigate(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.navigate(1);
  }

  onStatusChange(event: Event): void {
    this.navigate(1, (event.target as HTMLSelectElement).value);
  }

  private navigate(page: number, status?: string): void {
    const queryParams: Params = {
      page,
      size: this.pageSize() === 10 ? null : this.pageSize(),
      status: (status ?? this.statusFilter()) || null,
    };
    this.router.navigate([], { relativeTo: this.route, queryParams });
  }

  openEdit(e: EnrollmentSummary): void {
    this.router.navigate(['/enrollment', e.id, 'edit']);
  }

  applicantName(e: EnrollmentSummary): string {
    return this.i18n.lang() === 'ar'
      ? `${e.firstNameAr} ${e.familyNameAr}`
      : `${e.firstNameEn} ${e.familyNameEn}`;
  }
}
