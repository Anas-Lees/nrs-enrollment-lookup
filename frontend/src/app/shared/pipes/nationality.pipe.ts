import { Pipe, PipeTransform } from '@angular/core';

const NATIONALITY_NAMES: Record<string, string> = {
  OMN: 'Oman',
  ARE: 'United Arab Emirates',
  SAU: 'Saudi Arabia',
  KWT: 'Kuwait',
  QAT: 'Qatar',
  BHR: 'Bahrain',
  IND: 'India',
  PAK: 'Pakistan',
  BGD: 'Bangladesh',
  PHL: 'Philippines',
  EGY: 'Egypt',
  GBR: 'United Kingdom',
  USA: 'United States',
  YEM: 'Yemen',
  JOR: 'Jordan',
  LKA: 'Sri Lanka',
};

@Pipe({
  name: 'nationality',
})
export class NationalityPipe implements PipeTransform {
  transform(code: string | null | undefined): string {
    if (code === null || code === undefined) {
      return '';
    }
    return NATIONALITY_NAMES[code] ?? code;
  }
}
