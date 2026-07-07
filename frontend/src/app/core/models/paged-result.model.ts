/** Pagination envelope returned by the search endpoint. */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Inbound search filters (all optional). */
export interface PersonSearchCriteria {
  crn?: string;
  name?: string;
  dob?: string;
  nationality?: string;
  /** Result ordering: name-asc (default), name-desc, dob-desc, dob-asc, crn-asc. */
  sort?: string | null;
  page?: number;
  pageSize?: number;
}
