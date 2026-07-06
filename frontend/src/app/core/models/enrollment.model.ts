// TypeScript shapes mirroring the enrollment API DTOs (the frozen OpenAPI contract).
// Dates are ISO strings; enums are string unions matching the API.

export type EnrollmentType = 'NEW_CARD' | 'RENEWAL' | 'REPLACEMENT' | 'CORRECTION';
export type EnrollmentStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'PENDING_REVIEW'
  | 'UNDER_REVIEW'
  | 'NEEDS_CORRECTION'
  | 'APPROVED'
  | 'REJECTED'
  | 'WITHDRAWN';

/** Screening's risk verdict: HIGH reviews are supervisor-only. */
export type RiskLevel = 'HIGH' | 'NORMAL';

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
  /** Screening's risk verdict ("HIGH" reviews are supervisor-only), or null if unscreened. */
  riskLevel: RiskLevel | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  /** Set when the review breached its SLA and a supervisor was notified. */
  escalatedAtUtc: string | null;
}

/** A full enrollment application. */
export interface Enrollment extends EnrollmentSummary {
  dateOfBirth: string;
  /** "M"/"F", captured so an approved new applicant can be registered as a person. */
  gender: string | null;
  notes: string | null;
  createdBy: string;
  /** When the current assignee claimed it; null while unassigned. */
  assignedAtUtc: string | null;
  /** Who decided (a reviewer, or "auto-screening"), once the review concluded. */
  decidedBy: string | null;
  decidedAtUtc: string | null;
  decisionNotes: string | null;
  screeningFlags: ScreeningFlag[];
  /** The reviewer's note on what to fix, while the application sits in NEEDS_CORRECTION. */
  correctionNote: string | null;
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
  gender?: string | null;
  nationalityCode: string;
  type: EnrollmentType;
  notes?: string | null;
}
