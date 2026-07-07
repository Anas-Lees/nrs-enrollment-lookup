/** One of Oman's 11 governorates: the English value stored in the record + its Arabic label. */
export interface GovernorateOption {
  value: string;
  ar: string;
}

/** The 11 governorates of Oman (English name is the stored value; Arabic shown in RTL). */
export const OMAN_GOVERNORATES: readonly GovernorateOption[] = [
  { value: 'Muscat', ar: 'مسقط' },
  { value: 'Dhofar', ar: 'ظفار' },
  { value: 'Musandam', ar: 'مسندم' },
  { value: 'Al Buraimi', ar: 'البريمي' },
  { value: 'Ad Dakhiliyah', ar: 'الداخلية' },
  { value: 'Al Batinah North', ar: 'شمال الباطنة' },
  { value: 'Al Batinah South', ar: 'جنوب الباطنة' },
  { value: 'Ash Sharqiyah North', ar: 'شمال الشرقية' },
  { value: 'Ash Sharqiyah South', ar: 'جنوب الشرقية' },
  { value: 'Adh Dhahirah', ar: 'الظاهرة' },
  { value: 'Al Wusta', ar: 'الوسطى' },
];
