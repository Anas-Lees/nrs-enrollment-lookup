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
  private readonly realmRoles = signal<string[]>([]);

  /** True when auth is off, or when a valid session exists. */
  readonly isAuthenticated = computed(() => !this.enabled || this.authed());

  /**
   * The signed-in user's realm roles. With auth disabled (open POC) every role is granted,
   * so one person can walk the whole operator → reviewer → supervisor journey.
   */
  readonly roles = computed<string[]>(() =>
    this.enabled ? this.realmRoles() : ['operator', 'reviewer', 'supervisor'],
  );

  /** Reviewers decide applications; the review screen and queue actions are theirs. */
  readonly isReviewer = computed(() => this.roles().includes('reviewer'));

  /** Supervisors own escalations and the analytics dashboard. */
  readonly isSupervisor = computed(() => this.roles().includes('supervisor'));

  /**
   * The username the API sees for this session. With auth off the API resolves every caller
   * as "anonymous", so we must match it — otherwise claim states ("mine" vs "other") would
   * never line up in POC mode.
   */
  readonly username = computed(() =>
    this.enabled ? (this.tokenUsername() ?? 'operator') : 'anonymous',
  );

  private readonly tokenUsername = signal<string | null>(null);

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
    this.readTokenClaims();
  }

  /** Pull roles + username out of the parsed access token. */
  private readTokenClaims(): void {
    const parsed = this.keycloak?.tokenParsed as
      { realm_access?: { roles?: string[] }; preferred_username?: string } | undefined;
    this.realmRoles.set(parsed?.realm_access?.roles ?? []);
    this.tokenUsername.set(parsed?.preferred_username ?? null);
  }

  getToken(): string | null {
    return this.keycloak?.token ?? null;
  }

  /**
   * Returns a valid access token, refreshing it first if it is within 30s of expiry.
   * Keycloak access tokens are short-lived (5 min by default), so without this a session
   * that idles past the lifespan would 401 every API call. If the refresh fails (e.g. the
   * SSO session itself has ended) the operator is sent to log in again.
   */
  async freshToken(): Promise<string | null> {
    if (!this.keycloak) {
      return null;
    }
    try {
      await this.keycloak.updateToken(30);
      this.readTokenClaims();
    } catch {
      void this.keycloak.login();
      return null;
    }
    return this.keycloak.token ?? null;
  }

  login(): void {
    void this.keycloak?.login();
  }

  logout(): void {
    void this.keycloak?.logout();
  }
}
