import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../config/app-config';
import { CardTask } from '../models/card-office.model';

/**
 * Talks to the card office API — the physical fulfilment of an approved application. Cards move
 * IN_PRODUCTION → READY_FOR_COLLECTION → ACTIVE as the office prints and hands them over.
 */
@Injectable({ providedIn: 'root' })
export class CardOfficeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${APP_CONFIG.apiBaseUrl}/api/v1/card-office`;

  /** Cards in production and awaiting collection, oldest first. */
  list(): Observable<CardTask[]> {
    return this.http.get<CardTask[]>(this.baseUrl);
  }

  /** Mark a card printed — it becomes ready for collection. */
  markPrinted(cardId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${cardId}/printed`, {});
  }

  /** Mark a card collected by the applicant — it becomes active. */
  markCollected(cardId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${cardId}/collected`, {});
  }
}
