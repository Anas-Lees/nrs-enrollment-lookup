import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';

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
  private readonly route = inject(ActivatedRoute);

  private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');

  readonly form = new FormGroup({
    query: new FormControl('', { nonNullable: true }),
    crn: new FormControl('', { nonNullable: true }),
    name: new FormControl('', { nonNullable: true }),
    dob: new FormControl('', { nonNullable: true }),
    nationality: new FormControl('', { nonNullable: true }),
  });

  readonly results = signal<PagedResult<PersonSummary> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searched = signal(false);
  readonly elapsedMs = signal(0);
  readonly advancedOpen = signal(false);

  // Quick-preview state for the right-hand panel.
  readonly selectedCrn = signal<string | null>(null);
  readonly preview = signal<Person | null>(null);
  readonly previewLoading = signal(false);

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

  constructor() {
    // The URL query string is the source of truth: drives the search and makes
    // results shareable/bookmarkable and the back button work.
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      const advanced = pm.get('adv') === '1';
      this.advancedOpen.set(advanced);
      this.form.setValue(
        {
          query: pm.get('q') ?? '',
          crn: pm.get('crn') ?? '',
          name: pm.get('name') ?? '',
          dob: pm.get('dob') ?? '',
          nationality: pm.get('nat') ?? '',
        },
        { emitEvent: false },
      );

      const page = Number(pm.get('page')) || 1;
      const hasFilters =
        pm.has('q') || pm.has('crn') || pm.has('name') || pm.has('dob') || pm.has('nat');
      if (hasFilters || pm.has('page')) {
        this.criteria = this.buildCriteria();
        this.searched.set(true);
        this.load(page);
      }
    });
  }

  /**
   * Build the search criteria. In advanced mode the explicit CRN / name / DOB /
   * nationality fields all combine (AND). In quick mode the single smart box is
   * classified as a CRN, a date, or a name; nationality still applies.
   */
  private buildCriteria(): PersonSearchCriteria {
    const v = this.form.getRawValue();
    const criteria: PersonSearchCriteria = {};

    if (this.advancedOpen()) {
      if (v.crn.trim()) criteria.crn = v.crn.trim();
      if (v.name.trim()) criteria.name = v.name.trim();
      if (v.dob.trim()) criteria.dob = v.dob.trim();
    } else {
      const q = v.query.trim();
      if (q) {
        if (/^\d{4}-\d{2}-\d{2}$/.test(q)) {
          criteria.dob = q;
        } else if (/^\d{1,9}$/.test(q)) {
          criteria.crn = q;
        } else {
          criteria.name = q;
        }
      }
    }

    if (v.nationality.trim()) {
      criteria.nationality = v.nationality.trim();
    }
    return criteria;
  }

  toggleAdvanced(): void {
    this.advancedOpen.update((open) => !open);
  }

  onSubmit(): void {
    this.navigateToSearch(1);
  }

  clear(): void {
    this.form.reset({ query: '', crn: '', name: '', dob: '', nationality: '' });
    this.criteria = {};
    this.results.set(null);
    this.error.set(null);
    this.searched.set(false);
    this.clearPreview();
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  onPageChange(page: number): void {
    this.navigateToSearch(page);
  }

  /** Reflect the current form + mode into the URL; the subscription runs the search. */
  private navigateToSearch(page: number): void {
    const v = this.form.getRawValue();
    const advanced = this.advancedOpen();
    const queryParams: Params = {
      page,
      adv: advanced ? 1 : null,
      nat: v.nationality.trim() || null,
      q: advanced ? null : v.query.trim() || null,
      crn: advanced ? v.crn.trim() || null : null,
      name: advanced ? v.name.trim() || null : null,
      dob: advanced ? v.dob.trim() || null : null,
    };
    this.router.navigate([], { relativeTo: this.route, queryParams });
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

  // --- keyboard navigation: '/' focuses search, arrows move selection, Enter opens ---
  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key === '/' && !this.isTyping(event.target)) {
      event.preventDefault();
      this.searchInput()?.nativeElement.focus();
      return;
    }
    if (this.isTyping(event.target)) {
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.moveSelection(1);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.moveSelection(-1);
    } else if (event.key === 'Enter') {
      const crn = this.selectedCrn();
      if (crn) {
        this.router.navigate(['/persons', crn]);
      }
    }
  }

  private isTyping(target: EventTarget | null): boolean {
    const tag = (target as HTMLElement | null)?.tagName;
    return tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA';
  }

  private moveSelection(delta: number): void {
    const r = this.results();
    if (!r || r.items.length === 0) {
      return;
    }
    const current = r.items.findIndex((p) => p.civilNumber === this.selectedCrn());
    const next = Math.max(0, Math.min(r.items.length - 1, (current < 0 ? 0 : current) + delta));
    this.select(r.items[next]);
    queueMicrotask(() =>
      document.querySelector('.card.is-selected')?.scrollIntoView({ block: 'nearest' }),
    );
  }

  /** Select a row → load the full record into the quick-preview panel. */
  select(p: PersonSummary): void {
    if (this.selectedCrn() === p.civilNumber && this.preview()) {
      return;
    }
    this.selectedCrn.set(p.civilNumber);
    this.preview.set(null);
    this.previewLoading.set(true);
    this.personService.getByCrn(p.civilNumber).subscribe({
      next: (full) => {
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
