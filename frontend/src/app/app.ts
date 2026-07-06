import { Component, computed, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';

import { TranslationService } from './core/i18n/translation.service';
import { AuthService } from './core/services/auth.service';
import { NotificationBell } from './shared/components/notification-bell';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NotificationBell],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly i18n = inject(TranslationService);
  protected readonly auth = inject(AuthService);

  /** Secondary nav shown for visual parity with the console; not yet implemented. */
  protected readonly stubNav = ['nav.reports'];

  /** True only when auth is enabled AND a session is actually established. */
  protected readonly authActive = computed(() => this.auth.enabled && this.auth.isAuthenticated());

  /** The signed-in user's name (falls back to the generic label in open POC mode). */
  protected readonly operatorName = computed(() =>
    this.authActive() ? this.auth.username() : this.i18n.t('operator.name'),
  );

  /** Honest operator-status label: never claim a JWT session that doesn't exist. */
  protected readonly operatorStatusKey = computed(() => {
    if (!this.auth.enabled) {
      return 'operator.status.unauthenticated';
    }
    return this.auth.isAuthenticated() ? 'operator.status' : 'operator.status.signedOut';
  });
}
