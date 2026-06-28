import { Injectable, computed, signal } from '@angular/core';
import Keycloak from 'keycloak-js';

import { APP_CONFIG } from '../config/app-config';

/**
 * Authentication via Keycloak (keycloak-js). Disabled by default
 * (environment.auth.enabled = false), in which case the app behaves as an open POC
 * and no Keycloak instance is created.
 *
 * When enabled, init() runs at app startup (APP_INITIALIZER) and silently restores
 * an existing session; the guard triggers an interactive login when needed and the
 * HTTP interceptor attaches the access token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly enabled = APP_CONFIG.auth.enabled;

  private readonly keycloak: Keycloak | null = this.enabled
    ? new Keycloak({
        url: APP_CONFIG.auth.url,
        realm: APP_CONFIG.auth.realm,
        clientId: APP_CONFIG.auth.clientId,
      })
    : null;

  private readonly authed = signal(false);

  /** True when auth is off, or when a valid session exists. */
  readonly isAuthenticated = computed(() => !this.enabled || this.authed());

  /** Restore an existing session without forcing a redirect. Called at startup. */
  async init(): Promise<void> {
    if (!this.keycloak) {
      return;
    }
    const authenticated = await this.keycloak.init({
      onLoad: 'check-sso',
      pkceMethod: 'S256',
      // The login-status iframe is unreliable under modern 3rd-party-cookie rules
      // (and hangs in headless browsers); rely on token expiry/refresh instead.
      checkLoginIframe: false,
    });
    this.authed.set(authenticated);
  }

  getToken(): string | null {
    return this.keycloak?.token ?? null;
  }

  login(): void {
    void this.keycloak?.login();
  }

  logout(): void {
    void this.keycloak?.logout();
  }
}
