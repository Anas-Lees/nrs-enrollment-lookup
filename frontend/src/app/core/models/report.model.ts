// Shapes mirroring the enrollment analytics API DTO (/api/v1/reports/enrollment-summary).

export interface FlagCount {
  flag: string;
  count: number;
}

export interface NameCount {
  name: string;
  count: number;
}

export interface DailyVolume {
  date: string;
  created: number;
  decided: number;
}

/** Operational KPIs for the enrollment review, over a rolling window of days. */
export interface EnrollmentReport {
  windowDays: number;
  total: number;
  byStatus: Record<string, number>;
  byType: Record<string, number>;
  decided: number;
  autoApproved: number;
  humanDecided: number;
  approved: number;
  rejected: number;
  autoApprovalRatePct: number;
  approvalRatePct: number;
  avgHoursToDecision: number | null;
  escalated: number;
  escalationRatePct: number;
  topFlags: FlagCount[];
  byReviewer: NameCount[];
  daily: DailyVolume[];
}
