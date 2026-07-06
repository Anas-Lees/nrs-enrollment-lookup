import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

/**
 * Gates the review screen to holders of the reviewer role. Runs after authGuard, so the
 * session already exists; a plain operator is bounced back to search rather than shown a
 * screen whose every action would 403.
 */
export const reviewerGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isReviewer() ? true : router.parseUrl('/search');
};
