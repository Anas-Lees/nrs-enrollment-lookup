import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';
import { EnrollmentService } from '../../core/services/enrollment.service';
import { PagedResult } from '../../core/models/paged-result.model';
import { EnrollmentStatus, EnrollmentSummary } from '../../core/models/enrollment.model';
import { Pagination } from '../../shared/components/pagination';
import { StatusBadge } from '../../shared/components/status-badge';
import { SortSelect, SortOption } from '../../shared/components/sort-select';
import { AppDatePipe } from '../../shared/app-date.pipe';
import { avatarColor, personInitials } from '../../shared/avatar';

/**
 * The operator's enrollment register: a paged, status-filterable list of applications, newest
 * first. It is a tracking view, not a decision surface — approvals and rejections belong to
 * reviewers on the Review Tasks screen. Clicking a row opens the read-only detail (where a
 * rejection's reason is shown); editing is a separate action, only while still editable.
 * URL-driven (page / size / status) so the view is shareable and bookmarkable.
 */
@Component({
  selector: 'app-enrollment-queue',
  imports: [RouterLink, Pagination, StatusBadge, SortSelect, AppDatePipe],
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
  readonly sortBy = signal('created-desc');

  readonly sortOptions: SortOption[] = [
    { value: 'created-desc', label: 'sort.newest' },
    { value: 'created-asc', label: 'sort.oldest' },
    { value: 'name-asc', label: 'sort.nameAsc' },
    { value: 'name-desc', label: 'sort.nameDesc' },
    { value: 'type-asc', label: 'sort.type' },
    { value: 'status-asc', label: 'sort.status' },
  ];

  readonly statuses: EnrollmentStatus[] = [
    'SUBMITTED',
    'PENDING_REVIEW',
    'UNDER_REVIEW',
    'NEEDS_CORRECTION',
    'APPROVED',
    'REJECTED',
    'WITHDRAWN',
    'DRAFT',
  ];

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      const size = Number(pm.get('size'));
      this.pageSize.set(this.pageSizeOptions.includes(size) ? size : 10);
      this.statusFilter.set((pm.get('status') as EnrollmentStatus | null) ?? '');
      const sort = pm.get('sort');
      this.sortBy.set(this.sortOptions.some((o) => o.value === sort) ? sort! : 'created-desc');
      this.load(Number(pm.get('page')) || 1);
    });

    // Live view: silently re-poll the current page so background status changes — a review
    // decision settling, an auto-approval, a claim by a reviewer — appear without a manual
    // refresh.
    const timer = setInterval(() => this.refresh(), 15_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  /** Reload the current page/filter with no loading flicker. */
  private refresh(): void {
    const page = this.results()?.page ?? 1;
    this.enrollments
      .list({
        status: this.statusFilter() || null,
        page,
        pageSize: this.pageSize(),
        sort: this.sortBy(),
      })
      .subscribe({
        next: (r) => {
          if (r.items.length === 0 && r.page > 1) {
            return; // page-emptied edge case; leave it to the next explicit load
          }
          this.results.set(r);
        },
        error: () => undefined, // a failed poll just retries next tick
      });
  }

  load(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.enrollments
      .list({
        status: this.statusFilter() || null,
        page,
        pageSize: this.pageSize(),
        sort: this.sortBy(),
      })
      .subscribe({
        next: (r) => {
          if (r.items.length === 0 && r.page > 1) {
            this.load(r.page - 1);
            return;
          }
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

  onSortChange(sort: string): void {
    this.sortBy.set(sort);
    this.navigate(1);
  }

  private navigate(page: number, status?: string): void {
    const queryParams: Params = {
      page,
      size: this.pageSize() === 10 ? null : this.pageSize(),
      status: (status ?? this.statusFilter()) || null,
      sort: this.sortBy() === 'created-desc' ? null : this.sortBy(),
    };
    this.router.navigate([], { relativeTo: this.route, queryParams });
  }

  openDetail(e: EnrollmentSummary): void {
    this.router.navigate(['/enrollment', e.id]);
  }

  openEdit(e: EnrollmentSummary): void {
    this.router.navigate(['/enrollment', e.id, 'edit']);
  }

  /** An application can still be edited until it has been decided (incl. while awaiting corrections). */
  canEdit(e: EnrollmentSummary): boolean {
    return (
      e.status === 'DRAFT' ||
      e.status === 'SUBMITTED' ||
      e.status === 'PENDING_REVIEW' ||
      e.status === 'NEEDS_CORRECTION'
    );
  }

  applicantName(e: EnrollmentSummary): string {
    return this.i18n.lang() === 'ar'
      ? `${e.firstNameAr} ${e.familyNameAr}`
      : `${e.firstNameEn} ${e.familyNameEn}`;
  }

  /** Coloured avatar initials for the applicant (language-aware, seeded by reference). */
  initials(e: EnrollmentSummary): string {
    return personInitials(
      e.firstNameEn,
      e.familyNameEn,
      e.firstNameAr,
      e.familyNameAr,
      this.i18n.lang(),
    );
  }

  color(e: EnrollmentSummary): string {
    return avatarColor(e.civilNumber ?? e.referenceNumber);
  }

  /** Escalated chip shows only while the application is still awaiting/under review. */
  showEscalated(e: EnrollmentSummary): boolean {
    return !!e.escalatedAtUtc && (e.status === 'PENDING_REVIEW' || e.status === 'UNDER_REVIEW');
  }
}
