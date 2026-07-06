import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';

import { TranslationService } from '../../core/i18n/translation.service';
import { ReportsService } from '../../core/services/reports.service';
import { EnrollmentReport } from '../../core/models/report.model';

interface Bar {
  label: string;
  value: number;
  pct: number; // 0–100 of the row's max, for the bar width
}

/**
 * The enrollment analytics dashboard — the in-app equivalent of a Camunda Optimize report,
 * tailored to this workflow. Headline KPIs plus a few dependency-free SVG/CSS charts, computed
 * server-side from the review data. Read-only; refreshes on a window change and on a light poll.
 */
@Component({
  selector: 'app-reports',
  imports: [],
  templateUrl: './reports.html',
  styleUrl: './reports.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Reports implements OnInit {
  protected readonly i18n = inject(TranslationService);
  private readonly reports = inject(ReportsService);

  readonly report = signal<EnrollmentReport | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly windowDays = signal(30);
  readonly windowOptions = [7, 30, 90];

  /** The status pipeline as proportioned bars. */
  readonly statusBars = computed<Bar[]>(() => this.toBars(this.report()?.byStatus));
  readonly typeBars = computed<Bar[]>(() => this.toBars(this.report()?.byType));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.reports.enrollmentSummary(this.windowDays()).subscribe({
      next: (r) => {
        this.report.set(r);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.i18n.t('reports.error'));
        this.loading.set(false);
      },
    });
  }

  setWindow(days: number): void {
    this.windowDays.set(days);
    this.load();
  }

  private toBars(map: Record<string, number> | undefined): Bar[] {
    if (!map) {
      return [];
    }
    const entries = Object.entries(map);
    const max = Math.max(1, ...entries.map(([, v]) => v));
    return entries
      .sort((a, b) => b[1] - a[1])
      .map(([label, value]) => ({ label, value, pct: (value / max) * 100 }));
  }

  /** Max of the flag/reviewer/daily series, for scaling their bars. */
  flagMax(): number {
    return Math.max(1, ...(this.report()?.topFlags ?? []).map((f) => f.count));
  }

  reviewerMax(): number {
    return Math.max(1, ...(this.report()?.byReviewer ?? []).map((r) => r.count));
  }

  dailyMax(): number {
    const d = this.report()?.daily ?? [];
    return Math.max(1, ...d.map((x) => Math.max(x.created, x.decided)));
  }

  /** Height in px for a daily bar of the given value (chart body ~120px). */
  barHeight(value: number): number {
    return Math.round((value / this.dailyMax()) * 120);
  }

  /** Short day label (e.g. "07-06" -> "06") for the x-axis. */
  dayLabel(date: string): string {
    return date.slice(8); // day-of-month
  }

  avgDecision(): string {
    const h = this.report()?.avgHoursToDecision;
    if (h == null) {
      return '—';
    }
    // Present sub-hour times in minutes, otherwise hours (the demo settles in seconds/minutes).
    return h < 1
      ? `${Math.round(h * 60)} ${this.i18n.t('reports.unit.min')}`
      : `${h} ${this.i18n.t('reports.unit.hr')}`;
  }
}
