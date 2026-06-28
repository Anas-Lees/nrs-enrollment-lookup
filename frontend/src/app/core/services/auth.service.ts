import { Injectable, computed, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

/**
 * Authentication state for the (stretch) Keycloak integration. Disabled by default
 * (environment.auth.enabled = false), in which case the app behaves as an open POC.
 *
 * A production build would replace login()/logout() with a real OIDC adapter
 * (e.g. keycloak-js or angular-oauth2-oidc) that performs the redirect flow and
 * keeps the access token fresh; everything else here stays the same.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly enabled = environment.auth.enabled;

  private readonly token = signal<string | null>(null);

  /** True when auth is off, or when a token is present. */
  readonly isAuthenticated = computed(() => !this.enabled || this.token() !== null);

  getToken(): string | null {
    return this.token();
  }

  setToken(token: string | null): void {
    this.token.set(token);
  }

  /** Placeholder for the OIDC redirect to Keycloak (not wired in this POC build). */
  login(): void {
    console.warn('[auth] Keycloak login is not wired in this build (stretch goal).');
  }

  logout(): void {
    this.token.set(null);
  }
}
