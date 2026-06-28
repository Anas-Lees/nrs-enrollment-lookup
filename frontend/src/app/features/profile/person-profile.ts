import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';
import { PersonService } from '../../core/services/person.service';
import { Person } from '../../core/models/person.model';
import { DocumentTable } from '../../shared/components/document-table';
import { StatusBadge } from '../../shared/components/status-badge';
import { avatarColor, personInitials } from '../../shared/avatar';

@Component({
  selector: 'app-person-profile',
  imports: [RouterLink, DatePipe, DocumentTable, StatusBadge],
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

  readonly initials = computed(() => {
    const p = this.person();
    return p ? personInitials(p.firstNameEn, p.familyNameEn) : '';
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
