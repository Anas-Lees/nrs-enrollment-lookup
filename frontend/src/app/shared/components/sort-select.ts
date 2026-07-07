import { ChangeDetectionStrategy, Component, inject, input, model } from '@angular/core';

import { TranslationService } from '../../core/i18n/translation.service';

/** One sort choice: a stable value plus an i18n key for its label. */
export interface SortOption {
  value: string;
  label: string;
}

/**
 * A small labelled "Sort by" dropdown, shared by the list screens (queue, review tasks, card
 * office). Two-way bound via the `value` model so the parent reacts to a change.
 */
@Component({
  selector: 'app-sort-select',
  imports: [],
  template: `
    <label class="sort-select">
      <span class="sort-select__label">{{ i18n.t('sort.label') }}</span>
      <select (change)="onChange($event)" [attr.aria-label]="i18n.t('sort.label')">
        @for (o of options(); track o.value) {
          <!-- Bind [selected] (not [value] on the select): reliably reflects the current
               choice regardless of when options render, so the control never reverts to the
               first option after a sort. -->
          <option [value]="o.value" [selected]="o.value === value()">{{ i18n.t(o.label) }}</option>
        }
      </select>
    </label>
  `,
  styles: [
    `
      .sort-select {
        display: inline-flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 0.85rem;
      }
      .sort-select__label {
        color: var(--color-text-muted, #64748b);
        white-space: nowrap;
      }
      .sort-select select {
        appearance: none;
        -webkit-appearance: none;
        padding: 0.35rem 1.9rem 0.35rem 0.7rem;
        border: 1px solid var(--color-border);
        border-radius: var(--radius-sm, 8px);
        background-color: var(--color-surface);
        color: var(--color-text);
        font: inherit;
        font-size: 0.85rem;
        cursor: pointer;
        /* Chevron sits just inside the trailing edge (never far from the text). */
        background-image: url('data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="%235b635e" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"/></svg>');
        background-repeat: no-repeat;
        background-position: right 0.6rem center;
      }
      .sort-select select:focus-visible {
        outline: 2px solid var(--rop-green-600, #1c6b41);
        outline-offset: 1px;
      }
      /* RTL: chevron and padding move to the leading (left) edge. */
      :host-context([dir='rtl']) .sort-select select {
        padding: 0.35rem 0.7rem 0.35rem 1.9rem;
        background-position: left 0.6rem center;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SortSelect {
  protected readonly i18n = inject(TranslationService);

  readonly options = input.required<SortOption[]>();
  readonly value = model<string>('');

  onChange(event: Event): void {
    this.value.set((event.target as HTMLSelectElement).value);
  }
}
