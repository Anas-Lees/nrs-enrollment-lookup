// TypeScript shapes mirroring the enrollment API DTOs (the frozen OpenAPI contract).
// Dates are ISO strings; enums are string unions matching the API.

export type EnrollmentType = 'NEW_CARD' | 'RENEWAL' | 'REPLACEMENT' | 'CORRECTION';
export type EnrollmentStatus =
  'DRAFT' | 'SUBMITTED' | 'PENDING_REVIEW' | 'UNDER_REVIEW' | 'APPROVED' | 'REJECTED';

/** Why automated screening routed an application to a human reviewer. */
export type ScreeningFlag =
  | 'CRN_NOT_FOUND'
  | 'REGISTRY_RECORD_NOT_ACTIVE'
  | 'NAME_MISMATCH'
  | 'DUPLICATE_PENDING'
  | 'MINOR_APPLICANT';

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
  /** Reviewer who has claimed it, or null while unassigned in the queue. */
  assignedTo: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  /** Set when the review breached its SLA and a supervisor was notified. */
  escalatedAtUtc: string | null;
}

/** A full enrollment application. */
export interface Enrollment extends EnrollmentSummary {
  dateOfBirth: string;
  notes: string | null;
  createdBy: string;
  /** When the current assignee claimed it; null while unassigned. */
  assignedAtUtc: string | null;
  /** Who decided (a reviewer, or "auto-screening"), once the review concluded. */
  decidedBy: string | null;
  decidedAtUtc: string | null;
  decisionNotes: string | null;
  screeningFlags: ScreeningFlag[];
}

/** One item in the reviewer's work queue: its enrollment (carrying status + assignee). */
export interface ReviewTask {
  /** Reviewer who has claimed it, or null while it sits unassigned in the queue. */
  assignee: string | null;
  /** When it entered the queue / was last touched. */
  queuedAtUtc: string;
  enrollment: Enrollment;
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
