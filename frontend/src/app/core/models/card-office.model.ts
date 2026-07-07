// Shapes mirroring the card-office API DTOs.
import { EnrollmentType } from './enrollment.model';

/** Production status of an issued card while it is being fulfilled. */
export type CardProductionStatus = 'IN_PRODUCTION' | 'READY_FOR_COLLECTION';

/** One card in production or awaiting collection, with its applicant details. */
export interface CardTask {
  idCardId: number;
  cardNumber: string;
  civilNumber: string;
  cardType: string;
  status: CardProductionStatus;
  enrollmentId: string;
  referenceNumber: string;
  firstNameEn: string;
  familyNameEn: string;
  firstNameAr: string;
  familyNameAr: string;
  enrollmentType: EnrollmentType;
}
