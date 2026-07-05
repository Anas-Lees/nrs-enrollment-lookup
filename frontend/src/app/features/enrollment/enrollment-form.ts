import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';
import { EnrollmentService } from '../../core/services/enrollment.service';
import { EnrollmentRequest, EnrollmentType } from '../../core/models/enrollment.model';
import { DateField } from '../../shared/components/date-field';

interface NationalityOption {
  code: string;
  label: string;
}

/**
 * Create a new enrollment application, or edit an existing one (same form). Edit mode is
 * entered when the route carries an :id; a ?crn query param pre-fills the civil number when
 * the operator started from an applicant's profile.
 */
@Component({
  selector: 'app-enrollment-form',
  imports: [ReactiveFormsModule, RouterLink, DateField],
  templateUrl: './enrollment-form.html',
  styleUrl: './enrollment-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EnrollmentForm {
  protected readonly i18n = inject(TranslationService);
  private readonly enrollments = inject(EnrollmentService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Set in edit mode; null when creating. */
  readonly id = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly isEdit = computed(() => this.id() !== null);

  readonly form = new FormGroup({
    civilNumber: new FormControl('', { nonNullable: true }),
    firstNameEn: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    familyNameEn: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    firstNameAr: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    familyNameAr: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    dateOfBirth: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    nationalityCode: new FormControl('OMN', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    type: new FormControl<EnrollmentType>('NEW_CARD', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
  });

  readonly types: EnrollmentType[] = ['NEW_CARD', 'RENEWAL', 'REPLACEMENT', 'CORRECTION'];

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
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.id.set(idParam);
      this.loadForEdit(idParam);
    }

    // Continuing from a profile: pre-fill the civil number.
    const crn = this.route.snapshot.queryParamMap.get('crn');
    if (crn && !idParam) {
      this.form.patchValue({ civilNumber: crn });
    }
  }

  private loadForEdit(id: string): void {
    this.loading.set(true);
    this.enrollments.get(id).subscribe({
      next: (e) => {
        this.form.patchValue({
          civilNumber: e.civilNumber ?? '',
          firstNameEn: e.firstNameEn,
          familyNameEn: e.familyNameEn,
          firstNameAr: e.firstNameAr,
          familyNameAr: e.familyNameAr,
          dateOfBirth: e.dateOfBirth,
          nationalityCode: e.nationalityCode,
          type: e.type,
          notes: e.notes ?? '',
        });
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.i18n.t('enroll.loadError'));
        this.loading.set(false);
      },
    });
  }

  invalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && c.touched;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const body: EnrollmentRequest = {
      civilNumber: v.civilNumber.trim() || null,
      firstNameEn: v.firstNameEn.trim(),
      familyNameEn: v.familyNameEn.trim(),
      firstNameAr: v.firstNameAr.trim(),
      familyNameAr: v.familyNameAr.trim(),
      dateOfBirth: v.dateOfBirth,
      nationalityCode: v.nationalityCode,
      type: v.type,
      notes: v.notes.trim() || null,
    };

    this.saving.set(true);
    this.error.set(null);

    const id = this.id();
    const request = id ? this.enrollments.update(id, body) : this.enrollments.create(body);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigate(['/enrollment/queue']);
      },
      error: () => {
        this.error.set(this.i18n.t('enroll.saveError'));
        this.saving.set(false);
      },
    });
  }
}
