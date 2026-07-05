// TypeScript shapes mirroring the enrollment API DTOs (the frozen OpenAPI contract).
// Dates are ISO strings; enums are string unions matching the API.

export type EnrollmentType = 'NEW_CARD' | 'RENEWAL' | 'REPLACEMENT' | 'CORRECTION';
export type EnrollmentStatus = 'DRAFT' | 'SUBMITTED' | 'UNDER_REVIEW' | 'APPROVED' | 'REJECTED';

/** One row in the enrollment queue. */
export interface EnrollmentSummary {
  id: string;
  referenceNumber: string;
  civilNumber: string | null;
  firstNameEn: string;
  familyNameEn: string;
  firstNameAr: string;
  familyNameAr: string;
  nationalityCode: string;
  type: EnrollmentType;
  status: EnrollmentStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** A full enrollment application. */
export interface Enrollment extends EnrollmentSummary {
  dateOfBirth: string;
  notes: string | null;
  createdBy: string;
}

/** Request body for creating or editing an enrollment application. */
export interface EnrollmentRequest {
  civilNumber?: string | null;
  firstNameEn: string;
  familyNameEn: string;
  firstNameAr: string;
  familyNameAr: string;
  dateOfBirth: string;
  nationalityCode: string;
  type: EnrollmentType;
  notes?: string | null;
}
