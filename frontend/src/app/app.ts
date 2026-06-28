import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';

import { TranslationService } from './core/i18n/translation.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly i18n = inject(TranslationService);

  /** Secondary nav shown for visual parity with the console; not yet implemented. */
  protected readonly stubNav = ['nav.newEnrollment', 'nav.myQueue', 'nav.reports'];
}
