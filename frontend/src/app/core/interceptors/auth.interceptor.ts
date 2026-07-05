import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap } from 'rxjs';

import { AuthService } from '../services/auth.service';

/**
 * Attaches the Keycloak bearer token to outgoing API calls when auth is enabled,
 * refreshing it first if it is near expiry so a long-lived session never sends an expired
 * token (which the API would 401). A no-op when auth is disabled (the default POC mode).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  if (!auth.enabled) {
    return next(req);
  }

  return from(auth.freshToken()).pipe(
    switchMap((token) => {
      const authedReq = token
        ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
        : req;
      return next(authedReq);
    }),
  );
};
