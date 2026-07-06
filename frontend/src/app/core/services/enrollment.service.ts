import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../config/app-config';
import { PagedResult } from '../models/paged-result.model';
import {
  Enrollment,
  EnrollmentRequest,
  EnrollmentStatus,
  EnrollmentSummary,
  ReviewTask,
} from '../models/enrollment.model';

/**
 * Talks to the enrollment API. The single place HTTP calls to /enrollments live, so
 * components depend on this service rather than on HttpClient directly.
 */
@Injectable({ providedIn: 'root' })
export class EnrollmentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${APP_CONFIG.apiBaseUrl}/api/v1/enrollments`;

  /** Paged list of enrollments, newest first, optionally filtered by status. */
  list(
    opts: { status?: EnrollmentStatus | null; page?: number; pageSize?: number } = {},
  ): Observable<PagedResult<EnrollmentSummary>> {
    let params = new HttpParams();
    if (opts.status) {
      params = params.set('status', opts.status);
    }
    if (opts.page != null) {
      params = params.set('page', opts.page);
    }
    if (opts.pageSize != null) {
      params = params.set('pageSize', opts.pageSize);
    }
    return this.http.get<PagedResult<EnrollmentSummary>>(this.baseUrl, { params });
  }

  /** Full enrollment application by id (used to load the edit form). */
  get(id: string): Observable<Enrollment> {
    return this.http.get<Enrollment>(`${this.baseUrl}/${encodeURIComponent(id)}`);
  }

  /** Create a new enrollment application. */
  create(body: EnrollmentRequest): Observable<Enrollment> {
    return this.http.post<Enrollment>(this.baseUrl, body);
  }

  /** Edit an existing enrollment application. */
  update(id: string, body: EnrollmentRequest): Observable<Enrollment> {
    return this.http.put<Enrollment>(`${this.baseUrl}/${encodeURIComponent(id)}`, body);
  }

  /**
   * Approve or reject an enrollment that is under review (reviewer role). The API completes
   * the Camunda review task, which applies the resulting status; the returned DTO reflects
   * it. A reason is required when rejecting.
   */
  decide(id: string, approved: boolean, notes: string | null): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/${encodeURIComponent(id)}/decision`, {
      approved,
      notes,
    });
  }

  /** Open review tasks (Camunda user tasks + their enrollments), oldest first. */
  listReviewTasks(): Observable<ReviewTask[]> {
    return this.http.get<ReviewTask[]>(`${APP_CONFIG.apiBaseUrl}/api/v1/review-tasks`);
  }

  /** Claim a review task for the signed-in reviewer. */
  claimReviewTask(userTaskKey: string): Observable<{ assignee: string }> {
    return this.http.post<{ assignee: string }>(
      `${APP_CONFIG.apiBaseUrl}/api/v1/review-tasks/${encodeURIComponent(userTaskKey)}/claim`,
      {},
    );
  }
}
