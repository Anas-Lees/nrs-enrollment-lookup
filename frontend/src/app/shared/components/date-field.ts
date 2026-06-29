import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  forwardRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

import { TranslationService } from '../../core/i18n/translation.service';
import {
  MONTHS_FULL,
  WEEKDAYS_SHORT,
  daysInMonth,
  firstWeekday,
  formatDisplay,
  parseIso,
  toIso,
} from '../date-util';

/**
 * Custom, bilingual date picker (replaces the native <input type="date">, whose calendar
 * language is dictated by the browser). Month/weekday names follow the app language; day
 * and year numbers stay Latin. RTL-aware (the grid flows from the document direction).
 * Implements ControlValueAccessor so it binds with formControlName; the value is an ISO
 * yyyy-mm-dd string (same shape the search criteria already use).
 */
@Component({
  selector: 'app-date-field',
  imports: [],
  templateUrl: './date-field.html',
  styleUrl: './date-field.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DateField), multi: true },
  ],
})
export class DateField implements ControlValueAccessor {
  protected readonly i18n = inject(TranslationService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly placeholder = input<string>('');
  readonly ariaLabel = input<string>('');

  readonly value = signal<string | null>(null); // ISO yyyy-mm-dd
  readonly open = signal(false);
  readonly disabled = signal(false);

  private readonly today = new Date();
  readonly viewYear = signal(this.today.getFullYear());
  readonly viewMonth = signal(this.today.getMonth() + 1); // 1-12

  private onChange: (v: string | null) => void = () => {};
  private onTouched: () => void = () => {};

  readonly display = computed(() => formatDisplay(this.value(), this.i18n.lang()));
  readonly monthLabel = computed(
    () => `${MONTHS_FULL[this.i18n.lang()][this.viewMonth() - 1]} ${this.viewYear()}`,
  );
  readonly weekdays = computed(() => WEEKDAYS_SHORT[this.i18n.lang()]);

  /** Leading blanks (to align the 1st under its weekday) followed by the day numbers. */
  readonly cells = computed<(number | null)[]>(() => {
    const y = this.viewYear();
    const m = this.viewMonth();
    const out: (number | null)[] = [];
    for (let i = 0; i < firstWeekday(y, m); i++) {
      out.push(null);
    }
    for (let d = 1; d <= daysInMonth(y, m); d++) {
      out.push(d);
    }
    return out;
  });

  readonly selectedDay = computed(() => {
    const p = parseIso(this.value());
    return p && p.y === this.viewYear() && p.m === this.viewMonth() ? p.d : null;
  });

  toggle(): void {
    if (this.disabled()) {
      return;
    }
    if (!this.open()) {
      const p = parseIso(this.value());
      if (p) {
        this.viewYear.set(p.y);
        this.viewMonth.set(p.m);
      }
    }
    this.open.update((o) => !o);
    this.onTouched();
  }

  shiftMonth(delta: number): void {
    let m = this.viewMonth() + delta;
    let y = this.viewYear();
    if (m < 1) {
      m = 12;
      y -= 1;
    } else if (m > 12) {
      m = 1;
      y += 1;
    }
    this.viewMonth.set(m);
    this.viewYear.set(y);
  }

  shiftYear(delta: number): void {
    this.viewYear.update((y) => y + delta);
  }

  pick(d: number): void {
    const iso = toIso(this.viewYear(), this.viewMonth(), d);
    this.value.set(iso);
    this.onChange(iso);
    this.open.set(false);
  }

  clear(event: Event): void {
    event.stopPropagation();
    this.value.set(null);
    this.onChange(null);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.open.set(false);
    }
  }

  // --- ControlValueAccessor ---
  writeValue(v: string | null): void {
    this.value.set(v || null);
  }
  registerOnChange(fn: (v: string | null) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
