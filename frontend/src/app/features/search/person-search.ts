import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';
import { PersonService } from '../../core/services/person.service';
import { PagedResult, PersonSearchCriteria } from '../../core/models/paged-result.model';
import { PersonSummary } from '../../core/models/person.model';
import { Pagination } from '../../shared/components/pagination';
import { StatusBadge } from '../../shared/components/status-badge';
import { avatarColor, personInitials } from '../../shared/avatar';

interface NationalityOption {
  code: string;
  label: string;
}

@Component({
  selector: 'app-person-search',
  imports: [ReactiveFormsModule, RouterLink, Pagination, StatusBadge, DatePipe],
  templateUrl: './person-search.html',
  styleUrl: './person-search.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonSearch {
  protected readonly i18n = inject(TranslationService);
  private readonly personService = inject(PersonService);
  private readonly router = inject(Router);

  readonly form = new FormGroup({
    query: new FormControl('', { nonNullable: true }),
    nationality: new FormControl('', { nonNullable: true }),
  });

  readonly results = signal<PagedResult<PersonSummary> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searched = signal(false);
  readonly elapsedMs = signal(0);

  private criteria: PersonSearchCriteria = {};
  readonly pageSize = 10;

  readonly totalPages = computed(() => {
    const r = this.results();
    return r ? Math.max(1, Math.ceil(r.totalCount / r.pageSize)) : 1;
  });

  readonly nationalityOptions: NationalityOption[] = [
    { code: 'OMN', label: 'Oman' },
    { code: 'ARE', label: 'United Arab Emirates' },
    { code: 'SAU', label: 'Saudi Arabia' },
    { code: 'KWT', label: 'Kuwait' },
    { code: 'QAT', label: 'Qatar' },
    { code: 'BHR', label: 'Bahrain' },
    { code: 'IND', label: 'India' },
    { code: 'PAK', label: 'Pakistan' },
    { code: 'BGD', label: 'Bangladesh' },
    { code: 'PHL', label: 'Philippines' },
    { code: 'EGY', label: 'Egypt' },
    { code: 'GBR', label: 'United Kingdom' },
    { code: 'USA', label: 'United States' },
  ];

  /** One smart box: detect whether the term is a CRN, a date, or a name. */
  private buildCriteria(query: string, nationality: string): PersonSearchCriteria {
    const criteria: PersonSearchCriteria = {};
    const q = query.trim();
    if (q) {
      if (/^\d{4}-\d{2}-\d{2}$/.test(q)) {
        criteria.dob = q;
      } else if (/^\d{1,9}$/.test(q)) {
        criteria.crn = q;
      } else {
        criteria.name = q;
      }
    }
    if (nationality.trim()) {
      criteria.nationality = nationality.trim();
    }
    return criteria;
  }

  onSubmit(): void {
    const { query, nationality } = this.form.getRawValue();
    this.criteria = this.buildCriteria(query, nationality);
    this.searched.set(true);
    this.load(1);
  }

  clear(): void {
    this.form.reset({ query: '', nationality: '' });
    this.criteria = {};
    this.results.set(null);
    this.error.set(null);
    this.searched.set(false);
  }

  load(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    const startedAt = performance.now();

    this.personService.search({ ...this.criteria, page, pageSize: this.pageSize }).subscribe({
      next: (r) => {
        this.elapsedMs.set(Math.round(performance.now() - startedAt));
        this.results.set(r);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.i18n.t('search.error'));
        this.loading.set(false);
      },
    });
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  openProfile(crn: string): void {
    this.router.navigate(['/persons', crn]);
  }

  // --- presentation helpers ---
  initials(p: PersonSummary): string {
    return personInitials(p.firstNameEn, p.familyNameEn);
  }

  color(p: PersonSummary): string {
    return avatarColor(p.civilNumber);
  }

  nationalityName(p: PersonSummary): string {
    const name = this.i18n.lang() === 'ar' ? p.nationalityNameAr : p.nationalityNameEn;
    return name ?? p.nationalityCode;
  }
}
