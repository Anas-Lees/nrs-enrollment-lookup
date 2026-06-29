import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeAr from '@angular/common/locales/ar';

import { routes } from './app.routes';

// Register Arabic locale data so DatePipe renders Arabic month names + Arabic-Indic
// digits when the UI language is Arabic. (en-US ships with Angular by default.)
registerLocaleData(localeAr);
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { correlationInterceptor } from './core/interceptors/correlation.interceptor';
import { AuthService } from './core/services/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([correlationInterceptor, authInterceptor])),
    // Restores a Keycloak session at startup when auth is enabled; no-op otherwise.
    provideAppInitializer(() => inject(AuthService).init()),
  ],
};
