import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Location } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { navigateBack } from '../../shared/navigate-back';

import { TranslationService } from '../../core/i18n/translation.service';
import { PersonService } from '../../core/services/person.service';
import { Person } from '../../core/models/person.model';
import { DocumentTable } from '../../shared/components/document-table';
import { StatusBadge } from '../../shared/components/status-badge';
import { AppDatePipe } from '../../shared/app-date.pipe';
import { avatarColor, personInitials } from '../../shared/avatar';
import { OMAN_GOVERNORATES, GovernorateOption } from '../../shared/oman';

/** The editable address fields, bound to the in-place address form. */
interface AddressForm {
  governorate: string;
  wilayat: string;
  village: string;
  street: string;
  buildingNumber: string;
  postalCode: string;
}

/** The editable contact fields, bound to the in-place contact form. */
interface ContactForm {
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
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly personService = inject(PersonService);

  readonly person = signal<Person | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal(false);

  // --- In-place editing (address and contact are edited independently, each in its own panel) ---
  readonly editingAddress = signal(false);
  readonly editingContact = signal(false);
  readonly savingAddress = signal(false);
  readonly savingContact = signal(false);
  readonly addressError = signal<string | null>(null);
  readonly contactError = signal<string | null>(null);

  readonly governorates = OMAN_GOVERNORATES;

  /** Bound to the in-place forms; seeded from the person on open. */
  addressForm: AddressForm = {
    governorate: '',
    wilayat: '',
    village: '',
    street: '',
    buildingNumber: '',
    postalCode: '',
  };
  contactForm: ContactForm = { mobile: '', email: '' };

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

  /** Back to wherever the operator came from (search, an enrollment, a card…). */
  goBack(): void {
    navigateBack(this.location, this.router, '/search');
  }

  // --- address + contact editing (each panel edits in place, independently) ---

  /** No address on file yet — flagged so the operator knows to complete it. */
  readonly missingAddress = computed(() => {
    const p = this.person();
    return !!p && p.address === null;
  });

  /** No contact on file yet. */
  readonly missingContact = computed(() => {
    const p = this.person();
    return !!p && p.contact === null;
  });

  /** The Arabic-or-English label for a governorate value. */
  governorateLabel(g: GovernorateOption): string {
    return this.i18n.lang() === 'ar' ? g.ar : g.value;
  }

  startEditAddress(): void {
    const a = this.person()?.address;
    this.addressForm = {
      governorate: a?.governorate ?? '',
      wilayat: a?.wilayat ?? '',
      village: a?.village ?? '',
      street: a?.street ?? '',
      buildingNumber: a?.buildingNumber ?? '',
      postalCode: a?.postalCode ?? '',
    };
    this.addressError.set(null);
    this.editingAddress.set(true);
  }

  cancelEditAddress(): void {
    this.editingAddress.set(false);
    this.addressError.set(null);
  }

  saveAddress(): void {
    const p = this.person();
    if (!p || this.savingAddress()) {
      return;
    }
    this.savingAddress.set(true);
    this.addressError.set(null);
    this.personService
      .updateAddress(p.civilNumber, {
        governorate: this.addressForm.governorate,
        wilayat: this.addressForm.wilayat.trim(),
        village: PersonProfile.orNull(this.addressForm.village),
        street: PersonProfile.orNull(this.addressForm.street),
        buildingNumber: PersonProfile.orNull(this.addressForm.buildingNumber),
        postalCode: PersonProfile.orNull(this.addressForm.postalCode),
      })
      .subscribe({
        next: (updated) => {
          this.person.set(updated);
          this.savingAddress.set(false);
          this.editingAddress.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.savingAddress.set(false);
          this.addressError.set(
            err.status === 400
              ? this.i18n.t('profile.edit.invalid')
              : this.i18n.t('profile.edit.error'),
          );
        },
      });
  }

  startEditContact(): void {
    const c = this.person()?.contact;
    this.contactForm = { mobile: c?.mobile ?? '', email: c?.email ?? '' };
    this.contactError.set(null);
    this.editingContact.set(true);
  }

  cancelEditContact(): void {
    this.editingContact.set(false);
    this.contactError.set(null);
  }

  saveContact(): void {
    const p = this.person();
    if (!p || this.savingContact()) {
      return;
    }
    this.savingContact.set(true);
    this.contactError.set(null);
    this.personService
      .updateContact(p.civilNumber, {
        mobile: PersonProfile.orNull(this.contactForm.mobile),
        email: PersonProfile.orNull(this.contactForm.email),
      })
      .subscribe({
        next: (updated) => {
          this.person.set(updated);
          this.savingContact.set(false);
          this.editingContact.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.savingContact.set(false);
          this.contactError.set(
            err.status === 400
              ? this.i18n.t('profile.edit.invalid')
              : this.i18n.t('profile.edit.error'),
          );
        },
      });
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
