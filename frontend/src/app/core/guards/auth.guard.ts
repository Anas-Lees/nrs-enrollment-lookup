import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';

import { AuthService } from '../services/auth.service';

/**
 * Blocks routes when auth is enabled and the user is not authenticated. Always
 * allows through when auth is disabled (the default POC mode).
 */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);

  if (!auth.enabled || auth.isAuthenticated()) {
    return true;
  }

  auth.login();
  return false;
};
