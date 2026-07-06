// Shapes mirroring the notification API DTOs.

/** One staff notification, written by the enrollment review workflow. */
export interface AppNotification {
  id: string;
  kind: 'review-queued' | 'decided' | 'escalated' | string;
  enrollmentId: string | null;
  referenceNumber: string | null;
  messageEn: string;
  messageAr: string;
  createdAtUtc: string;
  readAtUtc: string | null;
}

/** The bell payload: latest items plus the unread total for the badge. */
export interface NotificationList {
  items: AppNotification[];
  unreadCount: number;
}
