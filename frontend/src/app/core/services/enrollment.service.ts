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

  /** Paged list of enrollments, optionally filtered by status and sorted server-side. */
  list(
    opts: {
      status?: EnrollmentStatus | null;
      page?: number;
      pageSize?: number;
      sort?: string | null;
    } = {},
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
    if (opts.sort) {
      params = params.set('sort', opts.sort);
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
   * Approve or reject an enrollment you have claimed (assignee only). The API completes the
   * Camunda review task, which applies the resulting status; the returned DTO reflects it. A
   * reason is required when rejecting.
   */
  decide(id: string, approved: boolean, notes: string | null): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/${encodeURIComponent(id)}/decision`, {
      approved,
      notes,
    });
  }

  /**
   * Send an application you have claimed back to the operator for corrections (assignee only),
   * with a note describing what must be fixed. The application settles to NEEDS_CORRECTION.
   */
  requestCorrections(id: string, note: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(
      `${this.baseUrl}/${encodeURIComponent(id)}/request-corrections`,
      { note },
    );
  }

  /** Resubmit a corrected application (operator) — it re-enters screening and the queue. */
  resubmit(id: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/${encodeURIComponent(id)}/resubmit`, {});
  }

  /** Withdraw an application before it is decided (operator), with an optional reason. */
  withdraw(id: string, reason: string | null): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/${encodeURIComponent(id)}/withdraw`, {
      reason,
    });
  }

  /** Reviews in progress and waiting (pending + under review), oldest first. */
  listReviewTasks(): Observable<ReviewTask[]> {
    return this.http.get<ReviewTask[]>(`${APP_CONFIG.apiBaseUrl}/api/v1/review-tasks`);
  }

  /** Claim a pending review, taking ownership. Keyed by enrollment id. */
  claimReviewTask(enrollmentId: string): Observable<{ assignee: string }> {
    return this.http.post<{ assignee: string }>(
      `${APP_CONFIG.apiBaseUrl}/api/v1/review-tasks/${encodeURIComponent(enrollmentId)}/claim`,
      {},
    );
  }

  /** Release a review you hold back into the shared queue. */
  releaseReviewTask(enrollmentId: string): Observable<void> {
    return this.http.post<void>(
      `${APP_CONFIG.apiBaseUrl}/api/v1/review-tasks/${encodeURIComponent(enrollmentId)}/release`,
      {},
    );
  }
}
