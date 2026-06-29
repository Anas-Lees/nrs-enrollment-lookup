import { Component, computed, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';

import { TranslationService } from './core/i18n/translation.service';
import { AuthService } from './core/services/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthService);

  /** Secondary nav shown for visual parity with the console; not yet implemented. */
  protected readonly stubNav = ['nav.newEnrollment', 'nav.myQueue', 'nav.reports'];

  /** True only when auth is enabled AND a session is actually established. */
  protected readonly authActive = computed(() => this.auth.enabled && this.auth.isAuthenticated());

  /** Honest operator-status label: never claim a JWT session that doesn't exist. */
  protected readonly operatorStatusKey = computed(() => {
    if (!this.auth.enabled) {
      return 'operator.status.unauthenticated';
    }
    return this.auth.isAuthenticated() ? 'operator.status' : 'operator.status.signedOut';
  });
}
