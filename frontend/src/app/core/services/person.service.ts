import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../config/app-config';
import { PagedResult, PersonSearchCriteria } from '../models/paged-result.model';
import { Person, PersonSummary, UpdateContactDetailsRequest } from '../models/person.model';

/**
 * Talks to the Applicant Lookup API. The single place HTTP calls to /persons live,
 * so components depend on this service rather than on HttpClient directly.
 */
@Injectable({ providedIn: 'root' })
export class PersonService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${APP_CONFIG.apiBaseUrl}/api/v1/persons`;

  /** Paged, multi-filter search. Only the filters that are set are sent. */
  search(criteria: PersonSearchCriteria): Observable<PagedResult<PersonSummary>> {
    let params = new HttpParams();

    if (criteria.crn?.trim()) {
      params = params.set('crn', criteria.crn.trim());
    }
    if (criteria.name?.trim()) {
      params = params.set('name', criteria.name.trim());
    }
    if (criteria.dob?.trim()) {
      params = params.set('dob', criteria.dob.trim());
    }
    if (criteria.nationality?.trim()) {
      params = params.set('nationality', criteria.nationality.trim());
    }
    if (criteria.page != null) {
      params = params.set('page', criteria.page);
    }
    if (criteria.pageSize != null) {
      params = params.set('pageSize', criteria.pageSize);
    }

    return this.http.get<PagedResult<PersonSummary>>(`${this.baseUrl}/search`, { params });
  }

  /** Full profile (with documents) for one civil registration number. */
  getByCrn(crn: string): Observable<Person> {
    return this.http.get<Person>(`${this.baseUrl}/${encodeURIComponent(crn)}`);
  }

  /**
   * Sets a person's address and contact details, returning their refreshed profile. Used to
   * fill in the fields a freshly-registered applicant does not have yet, or to correct them.
   */
  updateContactDetails(crn: string, request: UpdateContactDetailsRequest): Observable<Person> {
    return this.http.put<Person>(
      `${this.baseUrl}/${encodeURIComponent(crn)}/contact-details`,
      request,
    );
  }
}
