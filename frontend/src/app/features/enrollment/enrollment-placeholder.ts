import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-enrollment-placeholder',
  imports: [RouterLink],
  templateUrl: './enrollment-placeholder.html',
  styleUrl: './enrollment-placeholder.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EnrollmentPlaceholder {
  protected readonly i18n = inject(TranslationService);
  private readonly route = inject(ActivatedRoute);

  /** CRN of the applicant we're enrolling from, if launched from a profile/preview. */
  readonly crn = toSignal(this.route.queryParamMap.pipe(map((pm) => pm.get('crn'))), {
    initialValue: null,
  });

  protected readonly steps = [
    'enroll.step.bio',
    'enroll.step.face',
    'enroll.step.fingerprints',
    'enroll.step.signature',
    'enroll.step.documents',
    'enroll.step.submit',
  ];
}
