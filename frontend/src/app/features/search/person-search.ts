import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { PersonService } from '../../core/services/person.service';
import { PagedResult, PersonSearchCriteria } from '../../core/models/paged-result.model';
import { PersonSummary } from '../../core/models/person.model';
import { Pagination } from '../../shared/components/pagination';
import { StatusBadge } from '../../shared/components/status-badge';
import { NationalityPipe } from '../../shared/pipes/nationality.pipe';

interface NationalityOption {
  code: string;
  label: string;
}

@Component({
  selector: 'app-person-search',
  imports: [ReactiveFormsModule, Pagination, StatusBadge, NationalityPipe, DatePipe],
  templateUrl: './person-search.html',
  styleUrl: './person-search.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonSearch {
  private readonly personService = inject(PersonService);
  private readonly router = inject(Router);

  readonly form = new FormGroup({
    crn: new FormControl('', { nonNullable: true }),
    name: new FormControl('', { nonNullable: true }),
    dob: new FormControl('', { nonNullable: true }),
    nationality: new FormControl('', { nonNullable: true }),
  });

  readonly results = signal<PagedResult<PersonSummary> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searched = signal(false);

  private criteria: PersonSearchCriteria = {};
  readonly pageSize = 10;

  readonly nationalityOptions: NationalityOption[] = [
    { code: '', label: 'Any nationality' },
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

  onSubmit(): void {
    const { crn, name, dob, nationality } = this.form.getRawValue();
    const criteria: PersonSearchCriteria = {};

    if (crn.trim()) {
      criteria.crn = crn.trim();
    }
    if (name.trim()) {
      criteria.name = name.trim();
    }
    if (dob.trim()) {
      criteria.dob = dob.trim();
    }
    if (nationality.trim()) {
      criteria.nationality = nationality.trim();
    }

    this.criteria = criteria;
    this.searched.set(true);
    this.load(1);
  }

  load(page: number): void {
    this.loading.set(true);
    this.error.set(null);

    this.personService
      .search({ ...this.criteria, page, pageSize: this.pageSize })
      .subscribe({
        next: (r) => {
          this.results.set(r);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Search failed. Please try again.');
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
}
