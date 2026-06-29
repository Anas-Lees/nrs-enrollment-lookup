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
import { DateField } from '../../shared/components/date-field';
import { AppDatePipe } from '../../shared/app-date.pipe';
import { avatarColor, personInitials } from '../../shared/avatar';

interface NationalityOption {
  code: string;
  label: string;
}

@Component({
  selector: 'app-person-search',
  imports: [ReactiveFormsModule, RouterLink, Pagination, StatusBadge, DateField, AppDatePipe],
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
    dob: new FormControl('', { nonNullable: true }),
    nationality: new FormControl('', { nonNullable: true }),
  });

  readonly results = signal<PagedResult<PersonSummary> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly elapsedMs = signal(0);

  // Quick-preview state for the right-hand panel.
  readonly selectedCrn = signal<string | null>(null);
  readonly preview = signal<Person | null>(null);
  readonly previewLoading = signal(false);

  private criteria: PersonSearchCriteria = {};
  readonly pageSize = signal(10);
  readonly pageSizeOptions = [10, 20, 50];

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
    // The URL query string is the source of truth: it drives the search and makes results
    // shareable/bookmarkable. With no params we still load the first page of everyone, so the
    // console always shows data instead of an empty state.
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.form.setValue(
        {
          query: pm.get('q') ?? '',
          dob: pm.get('dob') ?? '',
          nationality: pm.get('nat') ?? '',
        },
        { emitEvent: false },
      );
      const size = Number(pm.get('size'));
      this.pageSize.set(this.pageSizeOptions.includes(size) ? size : 10);
      this.criteria = this.buildCriteria();
      this.load(Number(pm.get('page')) || 1);
    });
  }

  /**
   * Build the search criteria from the single smart box: the term is classified as a date,
   * a CRN, or a name; the nationality filter applies on top. Empty term = match everyone.
   */
  private buildCriteria(): PersonSearchCriteria {
    const v = this.form.getRawValue();
    const criteria: PersonSearchCriteria = {};

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

    // Explicit date-of-birth picker (combines with the smart box; wins over a typed date).
    if (v.dob.trim()) {
      criteria.dob = v.dob.trim();
    }

    if (v.nationality.trim()) {
      criteria.nationality = v.nationality.trim();
    }
    return criteria;
  }

  onSubmit(): void {
    this.navigateToSearch(1);
  }

  clear(): void {
    this.form.reset({ query: '', dob: '', nationality: '' });
    // Navigate to a clean URL; the subscription reloads the default (all) results.
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  onPageChange(page: number): void {
    this.navigateToSearch(page);
  }

  /** Change how many results show per page; resets to the first page. */
  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.navigateToSearch(1);
  }

  /** Reflect the current form into the URL; the subscription runs the search. */
  private navigateToSearch(page: number): void {
    const v = this.form.getRawValue();
    const queryParams: Params = {
      page,
      q: v.query.trim() || null,
      dob: v.dob.trim() || null,
      nat: v.nationality.trim() || null,
      size: this.pageSize() === 10 ? null : this.pageSize(),
    };
    this.router.navigate([], { relativeTo: this.route, queryParams });
  }

  load(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    const startedAt = performance.now();

    this.personService.search({ ...this.criteria, page, pageSize: this.pageSize() }).subscribe({
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
      // Only the "open selected profile" shortcut, and only when Enter isn't activating a
      // focused control (a button/link/card handles its own Enter). Without this guard,
      // pressing Enter on e.g. the Clear or language-toggle button would also navigate away.
      const target = event.target as HTMLElement | null;
      if (target && target !== document.body) {
        return;
      }
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

  /**
   * Card click: the first click selects the row (loads the quick-preview); clicking the
   * already-selected row again opens its full profile. Mirrors the Enter-key shortcut.
   */
  onCardClick(p: PersonSummary): void {
    if (this.selectedCrn() === p.civilNumber) {
      this.router.navigate(['/persons', p.civilNumber]);
      return;
    }
    this.select(p);
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
    return personInitials(
      p.firstNameEn,
      p.familyNameEn,
      p.firstNameAr,
      p.familyNameAr,
      this.i18n.lang(),
    );
  }

  color(p: PersonSummary): string {
    return avatarColor(p.civilNumber);
  }

  nationalityName(p: PersonSummary): string {
    const name = this.i18n.lang() === 'ar' ? p.nationalityNameAr : p.nationalityNameEn;
    return name ?? p.nationalityCode;
  }
}
