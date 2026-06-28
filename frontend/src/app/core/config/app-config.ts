import { environment } from '../../../environments/environment';

export interface AuthConfig {
  enabled: boolean;
  url: string;
  realm: string;
  clientId: string;
}

export interface AppConfig {
  apiBaseUrl: string;
  auth: AuthConfig;
}

/**
 * Runtime configuration. Seeded with build-time defaults from `environment`, then
 * overwritten at startup from `/config.json` (served by nginx). This lets the SAME
 * built image be configured per environment (e.g. enable Keycloak in Docker/OpenShift)
 * without rebuilding — the 12-factor "config from the environment" approach.
 */
export const APP_CONFIG: AppConfig = {
  apiBaseUrl: environment.apiBaseUrl,
  auth: { ...environment.auth },
};

/** Loads /config.json and merges it over the defaults. Runs before app bootstrap. */
export async function loadAppConfig(): Promise<void> {
  try {
    const response = await fetch('config.json', { cache: 'no-cache' });
    if (response.ok) {
      const loaded = (await response.json()) as Partial<AppConfig>;
      if (loaded.apiBaseUrl !== undefined) {
        APP_CONFIG.apiBaseUrl = loaded.apiBaseUrl;
      }
      if (loaded.auth) {
        APP_CONFIG.auth = { ...APP_CONFIG.auth, ...loaded.auth };
      }
    }
  } catch {
    // No config.json (or unreadable) → keep the build-time defaults.
  }
}
