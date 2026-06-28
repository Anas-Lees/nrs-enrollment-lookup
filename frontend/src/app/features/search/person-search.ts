import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';
import { PersonService } from '../../core/services/person.service';
import { PagedResult, PersonSearchCriteria } from '../../core/models/paged-result.model';
import { Person, PersonSummary } from '../../core/models/person.model';
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

  // Quick-preview state for the right-hand panel.
  readonly selectedCrn = signal<string | null>(null);
  readonly preview = signal<Person | null>(null);
  readonly previewLoading = signal(false);
  readonly enrollNote = signal(false);

  private criteria: PersonSearchCriteria = {};
  readonly pageSize = 10;

  readonly totalPages = computed(() => {
    const r = this.results();
    return r ? Math.max(1, Math.ceil(r.totalCount / r.pageSize)) : 1;
  });

  readonly validIdCards = computed(
    () => this.preview()?.idCards.filter((c) => c.status === 'ACTIVE').length ?? 0,
  );
  readonly validPassports = computed(
    () => this.preview()?.passports.filter((p) => p.status === 'ACTIVE').length ?? 0,
  );
  readonly expiredPassports = computed(
    () => this.preview()?.passports.filter((p) => p.status === 'EXPIRED').length ?? 0,
  );

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
    this.clearPreview();
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
        // Auto-select the first result so the preview is never empty.
        if (r.items.length > 0) {
          this.select(r.items[0]);
        } else {
          this.clearPreview();
        }
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

  /** Select a row → load the full record into the quick-preview panel. */
  select(p: PersonSummary): void {
    this.enrollNote.set(false);
    if (this.selectedCrn() === p.civilNumber && this.preview()) {
      return;
    }
    this.selectedCrn.set(p.civilNumber);
    this.preview.set(null);
    this.previewLoading.set(true);
    this.personService.getByCrn(p.civilNumber).subscribe({
      next: (full) => {
        // Ignore a stale response if the selection changed meanwhile.
        if (this.selectedCrn() === full.civilNumber) {
          this.preview.set(full);
          this.previewLoading.set(false);
        }
      },
      error: () => this.previewLoading.set(false),
    });
  }

  private clearPreview(): void {
    this.selectedCrn.set(null);
    this.preview.set(null);
    this.previewLoading.set(false);
    this.enrollNote.set(false);
  }

  startEnrollment(): void {
    // New enrollment is outside the Applicant Lookup POC; show an honest note.
    this.enrollNote.set(true);
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
