import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

/**
 * Gates the Reports dashboard to holders of the supervisor role. Runs after authGuard, so the
 * session already exists; a non-supervisor is sent back to search rather than shown a screen
 * whose data request would 403.
 */
export const supervisorGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isSupervisor() ? true : router.parseUrl('/search');
};
