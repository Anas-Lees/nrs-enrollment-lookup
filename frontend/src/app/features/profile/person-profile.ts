import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';
import { PersonService } from '../../core/services/person.service';
import { Person, UpdateContactDetailsRequest } from '../../core/models/person.model';
import { DocumentTable } from '../../shared/components/document-table';
import { StatusBadge } from '../../shared/components/status-badge';
import { AppDatePipe } from '../../shared/app-date.pipe';
import { avatarColor, personInitials } from '../../shared/avatar';

/** Oman's 11 governorates: the stored English value plus the Arabic label to show in RTL. */
interface GovernorateOption {
  value: string;
  ar: string;
}

/** The editable address + contact fields, bound to the inline form. */
interface ContactForm {
  governorate: string;
  wilayat: string;
  village: string;
  street: string;
  buildingNumber: string;
  postalCode: string;
  mobile: string;
  email: string;
}

@Component({
  selector: 'app-person-profile',
  imports: [RouterLink, FormsModule, DocumentTable, StatusBadge, AppDatePipe],
  templateUrl: './person-profile.html',
  styleUrl: './person-profile.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonProfile {
  protected readonly i18n = inject(TranslationService);
  private readonly route = inject(ActivatedRoute);
  private readonly personService = inject(PersonService);

  readonly person = signal<Person | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal(false);

  // --- Address + contact editing ---
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);

  /** The 11 governorates of Oman (English value stored; Arabic label shown in RTL). */
  readonly governorates: readonly GovernorateOption[] = [
    { value: 'Muscat', ar: 'مسقط' },
    { value: 'Dhofar', ar: 'ظفار' },
    { value: 'Musandam', ar: 'مسندم' },
    { value: 'Al Buraimi', ar: 'البريمي' },
    { value: 'Ad Dakhiliyah', ar: 'الداخلية' },
    { value: 'Al Batinah North', ar: 'شمال الباطنة' },
    { value: 'Al Batinah South', ar: 'جنوب الباطنة' },
    { value: 'Ash Sharqiyah North', ar: 'شمال الشرقية' },
    { value: 'Ash Sharqiyah South', ar: 'جنوب الشرقية' },
    { value: 'Adh Dhahirah', ar: 'الظاهرة' },
    { value: 'Al Wusta', ar: 'الوسطى' },
  ];

  /** Bound to the inline edit form; seeded from the person on open. */
  form: ContactForm = PersonProfile.emptyForm();

  readonly initials = computed(() => {
    const p = this.person();
    // Reading i18n.lang() makes this recompute (to the right script) on a language toggle.
    return p
      ? personInitials(
          p.firstNameEn,
          p.familyNameEn,
          p.firstNameAr,
          p.familyNameAr,
          this.i18n.lang(),
        )
      : '';
  });

  readonly avatarColor = computed(() => {
    const p = this.person();
    return avatarColor(p?.civilNumber ?? '');
  });

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      const crn = pm.get('crn');
      if (crn === null) {
        this.loading.set(false);
        this.error.set(true);
        return;
      }
      this.load(crn);
    });
  }

  private load(crn: string): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.error.set(false);

    this.personService.getByCrn(crn).subscribe({
      next: (p) => {
        this.person.set(p);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err?.status === 404) {
          this.notFound.set(true);
        } else {
          this.error.set(true);
        }
      },
    });
  }

  // --- address + contact editing ---

  /** Whether the record has no address or contact yet — the freshly-registered case. */
  readonly missingContact = computed(() => {
    const p = this.person();
    return !!p && (p.address === null || p.contact === null);
  });

  /** The Arabic-or-English label for a governorate value (falls back to the raw value). */
  governorateLabel(g: GovernorateOption): string {
    return this.i18n.lang() === 'ar' ? g.ar : g.value;
  }

  /** Open the inline form, seeded from the current address/contact (blank if none yet). */
  startEdit(): void {
    const p = this.person();
    if (!p) {
      return;
    }
    this.form = {
      governorate: p.address?.governorate ?? '',
      wilayat: p.address?.wilayat ?? '',
      village: p.address?.village ?? '',
      street: p.address?.street ?? '',
      buildingNumber: p.address?.buildingNumber ?? '',
      postalCode: p.address?.postalCode ?? '',
      mobile: p.contact?.mobile ?? '',
      email: p.contact?.email ?? '',
    };
    this.saveError.set(null);
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.saveError.set(null);
  }

  save(): void {
    const p = this.person();
    if (!p || this.saving()) {
      return;
    }

    const request: UpdateContactDetailsRequest = {
      governorate: this.form.governorate,
      wilayat: this.form.wilayat.trim(),
      village: PersonProfile.orNull(this.form.village),
      street: PersonProfile.orNull(this.form.street),
      buildingNumber: PersonProfile.orNull(this.form.buildingNumber),
      postalCode: PersonProfile.orNull(this.form.postalCode),
      mobile: PersonProfile.orNull(this.form.mobile),
      email: PersonProfile.orNull(this.form.email),
    };

    this.saving.set(true);
    this.saveError.set(null);
    this.personService.updateContactDetails(p.civilNumber, request).subscribe({
      next: (updated) => {
        this.person.set(updated);
        this.saving.set(false);
        this.editing.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.saveError.set(
          err.status === 400
            ? this.i18n.t('profile.edit.invalid')
            : this.i18n.t('profile.edit.error'),
        );
      },
    });
  }

  private static emptyForm(): ContactForm {
    return {
      governorate: '',
      wilayat: '',
      village: '',
      street: '',
      buildingNumber: '',
      postalCode: '',
      mobile: '',
      email: '',
    };
  }

  private static orNull(value: string): string | null {
    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  // --- localisation helpers ---

  /** Pick the value for the active language, falling back to the other script. */
  pick(en: string | null, ar: string | null): string {
    const v = this.i18n.lang() === 'ar' ? (ar ?? en) : (en ?? ar);
    return v ?? this.i18n.t('profile.notRecorded');
  }

  value(v: string | null | undefined): string {
    return v && v.length > 0 ? v : this.i18n.t('profile.notRecorded');
  }

  genderLabel(gender: string): string {
    return this.i18n.t(`gender.${gender}`);
  }

  maritalLabel(status: string | null): string {
    return status ? this.i18n.t(`marital.${status}`) : this.i18n.t('profile.notRecorded');
  }

  nationalityName(p: Person): string {
    const name = this.i18n.lang() === 'ar' ? p.nationalityNameAr : p.nationalityNameEn;
    return name ?? p.nationalityCode;
  }
}
