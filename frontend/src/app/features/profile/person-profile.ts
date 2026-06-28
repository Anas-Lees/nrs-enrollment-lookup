import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { PersonService } from '../../core/services/person.service';
import { Person } from '../../core/models/person.model';
import { DocumentTable } from '../../shared/components/document-table';
import { StatusBadge } from '../../shared/components/status-badge';
import { NationalityPipe } from '../../shared/pipes/nationality.pipe';

const AVATAR_PALETTE = ['#1f6feb', '#0b8457', '#a83279', '#b8860b', '#5b4b8a', '#0b6e75'];

@Component({
  selector: 'app-person-profile',
  imports: [RouterLink, DatePipe, DocumentTable, StatusBadge, NationalityPipe],
  templateUrl: './person-profile.html',
  styleUrl: './person-profile.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonProfile {
  private readonly route = inject(ActivatedRoute);
  private readonly personService = inject(PersonService);

  readonly person = signal<Person | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal(false);

  readonly initials = computed(() => {
    const p = this.person();
    if (!p) {
      return '';
    }
    return `${p.firstNameEn.charAt(0)}${p.familyNameEn.charAt(0)}`.toUpperCase();
  });

  readonly avatarColor = computed(() => {
    const p = this.person();
    if (!p) {
      return AVATAR_PALETTE[0];
    }
    const hash = [...p.civilNumber].reduce((sum, ch) => sum + ch.charCodeAt(0), 0);
    return AVATAR_PALETTE[hash % AVATAR_PALETTE.length];
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
}
