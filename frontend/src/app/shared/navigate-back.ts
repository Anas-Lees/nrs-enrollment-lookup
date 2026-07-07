import { Location } from '@angular/common';
import { Router } from '@angular/router';

/**
 * Go back to the previous page the user was on — whatever it was. Falls back to a sensible
 * in-app route when there is no history to return to (e.g. the page was opened from a deep
 * link, a notification, or a fresh tab), so "Back" never strands the user or leaves the app.
 */
export function navigateBack(location: Location, router: Router, fallback: string): void {
  // history.length > 1 means the browser has a prior entry to return to in this session.
  if (typeof history !== 'undefined' && history.length > 1) {
    location.back();
  } else {
    router.navigateByUrl(fallback);
  }
}
