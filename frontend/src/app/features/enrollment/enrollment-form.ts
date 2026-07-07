import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Location } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';
import { EnrollmentService } from '../../core/services/enrollment.service';
import {
  EnrollmentRequest,
  EnrollmentType,
  MaritalStatus,
  PassportType,
} from '../../core/models/enrollment.model';
import { DateField } from '../../shared/components/date-field';
import { navigateBack } from '../../shared/navigate-back';
import { OMAN_GOVERNORATES, GovernorateOption } from '../../shared/oman';

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
  private readonly location = inject(Location);

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
    gender: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    nationalityCode: new FormControl('OMN', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    type: new FormControl<EnrollmentType>('NEW_CARD', {
      nonNullable: true,
      validators: [Validators.required],
    }),

    // --- Biographic (required essentials + optional extras) ---
    placeOfBirthEn: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(80)],
    }),
    placeOfBirthAr: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(80)],
    }),
    motherNameEn: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(150)],
    }),
    motherNameAr: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(150)],
    }),
    maritalStatus: new FormControl<MaritalStatus | ''>('', { nonNullable: true }),
    bloodType: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(3)] }),
    occupationEn: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(100)],
    }),
    occupationAr: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(100)],
    }),

    // --- Address ---
    governorate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    wilayat: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)],
    }),
    village: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(80)] }),
    street: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(120)] }),
    buildingNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(20)],
    }),
    postalCode: new FormControl('', {
      nonNullable: true,
      validators: [Validators.pattern(/^[0-9]{3,10}$/)],
    }),

    // --- Contact ---
    mobile: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\+?[0-9][0-9\s]{6,18}$/)],
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.email, Validators.maxLength(120)],
    }),

    // --- Passport (optional) ---
    passportNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(20)],
    }),
    passportType: new FormControl<PassportType | ''>('', { nonNullable: true }),
    passportIssueDate: new FormControl('', { nonNullable: true }),
    passportExpiryDate: new FormControl('', { nonNullable: true }),

    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
  });

  readonly types: EnrollmentType[] = ['NEW_CARD', 'RENEWAL', 'REPLACEMENT', 'CORRECTION'];
  readonly governorates: readonly GovernorateOption[] = OMAN_GOVERNORATES;
  readonly maritalStatuses: MaritalStatus[] = ['SINGLE', 'MARRIED', 'DIVORCED', 'WIDOWED'];
  readonly passportTypes: PassportType[] = [
    'ORDINARY',
    'DIPLOMATIC',
    'SERVICE',
    'SPECIAL',
    'ROYAL_DIPLOMATIC',
  ];

  /** The Arabic-or-English label for a governorate value. */
  governorateLabel(g: GovernorateOption): string {
    return this.i18n.lang() === 'ar' ? g.ar : g.value;
  }

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
          gender: e.gender ?? '',
          nationalityCode: e.nationalityCode,
          type: e.type,
          placeOfBirthEn: e.placeOfBirthEn ?? '',
          placeOfBirthAr: e.placeOfBirthAr ?? '',
          motherNameEn: e.motherNameEn ?? '',
          motherNameAr: e.motherNameAr ?? '',
          maritalStatus: e.maritalStatus ?? '',
          bloodType: e.bloodType ?? '',
          occupationEn: e.occupationEn ?? '',
          occupationAr: e.occupationAr ?? '',
          governorate: e.governorate ?? '',
          wilayat: e.wilayat ?? '',
          village: e.village ?? '',
          street: e.street ?? '',
          buildingNumber: e.buildingNumber ?? '',
          postalCode: e.postalCode ?? '',
          mobile: e.mobile ?? '',
          email: e.email ?? '',
          passportNumber: e.passportNumber ?? '',
          passportType: e.passportType ?? '',
          passportIssueDate: e.passportIssueDate ?? '',
          passportExpiryDate: e.passportExpiryDate ?? '',
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

  /** Back to wherever the operator came from (falls back to the queue on a deep link). */
  goBack(): void {
    navigateBack(this.location, this.router, '/enrollment/queue');
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const clean = (s: string) => s.trim() || null;
    const body: EnrollmentRequest = {
      civilNumber: clean(v.civilNumber),
      firstNameEn: v.firstNameEn.trim(),
      familyNameEn: v.familyNameEn.trim(),
      firstNameAr: v.firstNameAr.trim(),
      familyNameAr: v.familyNameAr.trim(),
      dateOfBirth: v.dateOfBirth,
      gender: v.gender || null,
      nationalityCode: v.nationalityCode,
      type: v.type,
      placeOfBirthEn: clean(v.placeOfBirthEn),
      placeOfBirthAr: clean(v.placeOfBirthAr),
      motherNameEn: clean(v.motherNameEn),
      motherNameAr: clean(v.motherNameAr),
      maritalStatus: v.maritalStatus || null,
      bloodType: clean(v.bloodType),
      occupationEn: clean(v.occupationEn),
      occupationAr: clean(v.occupationAr),
      governorate: v.governorate || null,
      wilayat: clean(v.wilayat),
      village: clean(v.village),
      street: clean(v.street),
      buildingNumber: clean(v.buildingNumber),
      postalCode: clean(v.postalCode),
      mobile: clean(v.mobile),
      email: clean(v.email),
      passportNumber: clean(v.passportNumber),
      passportType: v.passportType || null,
      passportIssueDate: clean(v.passportIssueDate),
      passportExpiryDate: clean(v.passportExpiryDate),
      notes: clean(v.notes),
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
