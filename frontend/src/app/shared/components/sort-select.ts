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
      <select [value]="value()" (change)="onChange($event)" [attr.aria-label]="i18n.t('sort.label')">
        @for (o of options(); track o.value) {
          <option [value]="o.value">{{ i18n.t(o.label) }}</option>
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
      }
      .sort-select select {
        padding: 0.35rem 0.6rem;
        border: 1px solid var(--color-border);
        border-radius: var(--radius-sm, 8px);
        background: var(--color-surface);
        color: var(--color-text);
        font: inherit;
        font-size: 0.85rem;
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
