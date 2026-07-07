import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { reviewerGuard } from './core/guards/reviewer.guard';
import { supervisorGuard } from './core/guards/supervisor.guard';

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
    path: 'enrollment/:id',
    title: 'Enrollment details · NRS',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/enrollment/enrollment-detail').then((m) => m.EnrollmentDetail),
  },
  {
    path: 'review',
    title: 'Review tasks · NRS',
    canActivate: [authGuard, reviewerGuard],
    loadComponent: () => import('./features/review/review-tasks').then((m) => m.ReviewTasks),
  },
  {
    path: 'card-office',
    title: 'Card office · NRS',
    canActivate: [authGuard],
    loadComponent: () => import('./features/card-office/card-office').then((m) => m.CardOffice),
  },
  {
    path: 'reports',
    title: 'Reports · NRS',
    canActivate: [authGuard, supervisorGuard],
    loadComponent: () => import('./features/reports/reports').then((m) => m.Reports),
  },
  { path: '**', redirectTo: 'search' },
];
