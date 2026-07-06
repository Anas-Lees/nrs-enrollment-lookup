import { HttpClient, HttpParams } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';

import { APP_CONFIG } from '../config/app-config';
import { AppNotification, NotificationList } from '../models/notification.model';
import { AuthService } from './auth.service';

/**
 * The staff notification bell. Polls the API every 30 seconds (a deliberate, boring choice:
 * notifications here are minutes-scale — review queued, decision made, SLA breached — so
 * websockets/SSE would be complexity without benefit at this scale).
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly baseUrl = `${APP_CONFIG.apiBaseUrl}/api/v1/notifications`;

  readonly items = signal<AppNotification[]>([]);
  readonly unreadCount = signal(0);

  private timer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  /** Begin polling (called from the app shell once the session is up). Safe to call twice. */
  start(): void {
    if (this.timer) {
      return;
    }
    this.refresh();
    this.timer = setInterval(() => this.refresh(), 30_000);
  }

  stop(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  refresh(): void {
    if (!this.auth.isAuthenticated()) {
      return;
    }
    const params = new HttpParams().set('limit', 20);
    this.http.get<NotificationList>(this.baseUrl, { params }).subscribe({
      next: (list) => {
        this.items.set(list.items);
        this.unreadCount.set(list.unreadCount);
      },
      // Best-effort: a failed poll just tries again next tick.
      error: () => undefined,
    });
  }

  markRead(id: string): void {
    this.http.post(`${this.baseUrl}/${encodeURIComponent(id)}/read`, {}).subscribe({
      next: () => this.refresh(),
      error: () => undefined,
    });
  }

  markAllRead(): void {
    this.http.post(`${this.baseUrl}/read-all`, {}).subscribe({
      next: () => this.refresh(),
      error: () => undefined,
    });
  }
}
