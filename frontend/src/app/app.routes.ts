import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'search', pathMatch: 'full' },
  {
    path: 'search',
    title: 'Applicant search · NRS',
    loadComponent: () => import('./features/search/person-search').then((m) => m.PersonSearch),
  },
  {
    path: 'persons/:crn',
    title: 'Applicant profile · NRS',
    loadComponent: () => import('./features/profile/person-profile').then((m) => m.PersonProfile),
  },
  { path: '**', redirectTo: 'search' },
];
