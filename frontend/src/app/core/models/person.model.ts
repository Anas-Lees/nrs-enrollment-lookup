// TypeScript shapes mirroring the API DTOs (the frozen OpenAPI contract).
// Dates are ISO strings (yyyy-MM-dd); enums are string unions matching the API.

export type Gender = 'M' | 'F';
export type PersonStatus = 'ACTIVE' | 'DECEASED' | 'MERGED';
export type CardStatus = 'ACTIVE' | 'EXPIRED' | 'BLOCKED' | 'LOST';
export type CardType = 'OMANI' | 'RESIDENT' | 'GCC' | 'INVESTOR';
export type PassportType = 'ORDINARY' | 'DIPLOMATIC' | 'SERVICE' | 'SPECIAL' | 'ROYAL_DIPLOMATIC';
export type PassportStatus = 'ACTIVE' | 'EXPIRED' | 'CANCELLED' | 'LOST' | 'STOLEN';
export type MaritalStatus = 'SINGLE' | 'MARRIED' | 'DIVORCED' | 'WIDOWED';

/** One row in the search results table. */
export interface PersonSummary {
  civilNumber: string;
  firstNameAr: string;
  familyNameAr: string;
  firstNameEn: string;
  familyNameEn: string;
  dateOfBirth: string;
  gender: Gender;
  nationalityCode: string;
  nationalityNameEn: string | null;
  nationalityNameAr: string | null;
  status: PersonStatus;
}

export interface Address {
  governorate: string;
  wilayat: string;
  village: string | null;
  street: string | null;
  buildingNumber: string | null;
  postalCode: string | null;
}

export interface Contact {
  mobile: string | null;
  email: string | null;
}

export interface IdCard {
  idCardId: number;
  civilNumber: string;
  cardNumber: string;
  issueDate: string | null;
  expiryDate: string | null;
  status: CardStatus;
  cardType: CardType;
}

export interface Passport {
  passportId: number;
  civilNumber: string;
  passportNumber: string;
  passportType: PassportType;
  issueDate: string | null;
  expiryDate: string | null;
  status: PassportStatus;
}

/** Full applicant profile, including related documents. */
export interface Person extends PersonSummary {
  photoPath: string | null;
  placeOfBirthEn: string | null;
  placeOfBirthAr: string | null;
  motherNameEn: string | null;
  motherNameAr: string | null;
  maritalStatus: MaritalStatus | null;
  bloodType: string | null;
  occupationEn: string | null;
  occupationAr: string | null;
  address: Address | null;
  contact: Contact | null;
  idCards: IdCard[];
  passports: Passport[];
}
