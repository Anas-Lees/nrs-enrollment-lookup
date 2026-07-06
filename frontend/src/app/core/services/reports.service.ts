import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../config/app-config';
import { EnrollmentReport } from '../models/report.model';

/** Fetches the enrollment analytics used by the Reports dashboard. */
@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${APP_CONFIG.apiBaseUrl}/api/v1/reports`;

  /** Enrollment KPIs over a rolling window (days). */
  enrollmentSummary(days: number): Observable<EnrollmentReport> {
    const params = new HttpParams().set('days', days);
    return this.http.get<EnrollmentReport>(`${this.baseUrl}/enrollment-summary`, { params });
  }
}
