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
  page?: number;
  pageSize?: number;
}
