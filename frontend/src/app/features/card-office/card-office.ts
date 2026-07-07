import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { TranslationService } from '../../core/i18n/translation.service';
import { CardOfficeService } from '../../core/services/card-office.service';
import { NotificationService } from '../../core/services/notification.service';
import { CardTask } from '../../core/models/card-office.model';
import { SortSelect, SortOption } from '../../shared/components/sort-select';

/**
 * The card office's workspace: the physical fulfilment of an approved application. Cards waiting
 * to be printed (IN_PRODUCTION) and cards printed and waiting to be handed over
 * (READY_FOR_COLLECTION), driven by the card status so the queues are accurate and lag-free.
 * Marking a card printed / collected completes the matching Camunda user task and advances it.
 */
@Component({
  selector: 'app-card-office',
  imports: [NgTemplateOutlet, SortSelect],
  templateUrl: './card-office.html',
  styleUrl: './card-office.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardOffice implements OnInit {
  protected readonly i18n = inject(TranslationService);
  private readonly cards = inject(CardOfficeService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly tasks = signal<CardTask[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** Card id whose action is in flight. */
  readonly busy = signal<number | null>(null);

  readonly sortBy = signal('oldest');
  readonly sortOptions: SortOption[] = [
    { value: 'oldest', label: 'sort.oldest' },
    { value: 'newest', label: 'sort.newest' },
    { value: 'name-asc', label: 'sort.nameAsc' },
    { value: 'name-desc', label: 'sort.nameDesc' },
    { value: 'type', label: 'sort.type' },
  ];

  /** Cards being produced — the print queue. */
  readonly toPrint = computed(() =>
    this.sortCards(this.tasks().filter((t) => t.status === 'IN_PRODUCTION')),
  );

  /** Printed cards waiting for the applicant — the hand-over queue. */
  readonly toHandOver = computed(() =>
    this.sortCards(this.tasks().filter((t) => t.status === 'READY_FOR_COLLECTION')),
  );

  private sortCards(list: CardTask[]): CardTask[] {
    const name = (t: CardTask) =>
      (this.i18n.lang() === 'ar'
        ? `${t.familyNameAr} ${t.firstNameAr}`
        : `${t.familyNameEn} ${t.firstNameEn}`
      ).toLowerCase();
    const arr = [...list];
    switch (this.sortBy()) {
      case 'newest':
        return arr.sort((a, b) => b.idCardId - a.idCardId);
      case 'name-asc':
        return arr.sort((a, b) => name(a).localeCompare(name(b)));
      case 'name-desc':
        return arr.sort((a, b) => name(b).localeCompare(name(a)));
      case 'type':
        return arr.sort((a, b) => a.enrollmentType.localeCompare(b.enrollmentType));
      default:
        return arr.sort((a, b) => a.idCardId - b.idCardId); // oldest first
    }
  }

  ngOnInit(): void {
    this.load();
    const timer = setInterval(() => this.refresh(), 12_000);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.cards.list().subscribe({
      next: (tasks) => {
        this.tasks.set(tasks);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.i18n.t('cards.error'));
        this.loading.set(false);
      },
    });
  }

  private refresh(): void {
    if (this.busy() !== null) {
      return;
    }
    this.cards.list().subscribe({
      next: (tasks) => this.tasks.set(tasks),
      error: () => undefined,
    });
  }

  applicantName(t: CardTask): string {
    return this.i18n.lang() === 'ar'
      ? `${t.firstNameAr} ${t.familyNameAr}`
      : `${t.firstNameEn} ${t.familyNameEn}`;
  }

  viewDetails(t: CardTask): void {
    this.router.navigate(['/enrollment', t.enrollmentId]);
  }

  markPrinted(t: CardTask): void {
    this.act(t, this.cards.markPrinted(t.idCardId));
  }

  markCollected(t: CardTask): void {
    if (!window.confirm(this.i18n.t('cards.confirmCollect').replace('{ref}', t.referenceNumber))) {
      return;
    }
    this.act(t, this.cards.markCollected(t.idCardId));
  }

  private act(t: CardTask, action: import('rxjs').Observable<void>): void {
    if (this.busy() !== null) {
      return;
    }
    this.busy.set(t.idCardId);
    this.error.set(null);
    action.subscribe({
      next: () => {
        this.busy.set(null);
        // The card advanced out of this queue — drop it, then reconcile.
        this.tasks.update((list) => list.filter((x) => x.idCardId !== t.idCardId));
        this.notifications.refresh();
        setTimeout(() => this.refresh(), 1000);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(null);
        this.error.set(
          err.status === 409 || err.status === 404
            ? this.i18n.t('cards.moved')
            : this.i18n.t('cards.error'),
        );
        this.load();
      },
    });
  }
}
