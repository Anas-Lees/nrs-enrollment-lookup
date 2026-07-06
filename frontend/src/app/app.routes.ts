import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { reviewerGuard } from './core/guards/reviewer.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'search', pathMatch: 'full' },
  {
    path: 'search',
    title: 'Applicant search · NRS',
    canActivate: [authGuard],
    loadComponent: () => import('./features/search/person-search').then((m) => m.PersonSearch),
  },
  {
    path: 'persons/:crn',
    title: 'Applicant profile · NRS',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/person-profile').then((m) => m.PersonProfile),
  },
  {
    path: 'enrollment/queue',
    title: 'Enrollment queue · NRS',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/enrollment/enrollment-queue').then((m) => m.EnrollmentQueue),
  },
  {
    path: 'enrollment/new',
    title: 'New enrollment · NRS',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/enrollment/enrollment-form').then((m) => m.EnrollmentForm),
  },
  {
    path: 'enrollment/:id/edit',
    title: 'Edit enrollment · NRS',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/enrollment/enrollment-form').then((m) => m.EnrollmentForm),
  },
  {
    path: 'review',
    title: 'Review tasks · NRS',
    canActivate: [authGuard, reviewerGuard],
    loadComponent: () => import('./features/review/review-tasks').then((m) => m.ReviewTasks),
  },
  { path: '**', redirectTo: 'search' },
];
