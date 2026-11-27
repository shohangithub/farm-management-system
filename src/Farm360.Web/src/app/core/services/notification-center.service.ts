import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, of } from 'rxjs';
import { translate } from '../i18n/i18n.service';

/** A stored notification (e.g. a health reminder) as the API sends it: dictionary keys + parameters. */
export interface StoredNotification {
  id: string;
  type: 'Info' | 'Success' | 'Warning' | 'Alert' | 'SystemAnnouncement' | string;
  category?: string | null;
  title: string;
  body: string;
  params: Record<string, string>;
  link?: string | null;
  isRead: boolean;
  createdAtUtc: string;
  farmId?: string | null;
}

interface NotificationList {
  items: StoredNotification[];
  unreadCount: number;
}

/**
 * The header bell's stored notifications (daily health reminders): loaded from the API, kept live
 * by the SignalR hub ("NewNotification"), marked read one by one or all at once. Titles and bodies
 * are translated here from their English keys, so each user reads them in their own language.
 */
@Injectable({ providedIn: 'root' })
export class NotificationCenterService {
  private readonly http = inject(HttpClient);
  private readonly api = '/api/v1/notifications';

  readonly items = signal<StoredNotification[]>([]);
  readonly unreadCount = signal(0);
  readonly loaded = signal(false);
  readonly hasUnread = computed(() => this.unreadCount() > 0);

  load(): void {
    this.http.get<NotificationList>(this.api, { params: { take: 30 } })
      .pipe(catchError(() => of<NotificationList | null>(null)))
      .subscribe(list => {
        if (!list) return;
        this.items.set(list.items ?? []);
        this.unreadCount.set(list.unreadCount ?? 0);
        this.loaded.set(true);
      });
  }

  /** A notification pushed live by the hub. */
  receive(notification: StoredNotification): void {
    if (this.items().some(n => n.id === notification.id)) return;
    this.items.update(list => [notification, ...list].slice(0, 50));
    if (!notification.isRead) this.unreadCount.update(c => c + 1);
  }

  markRead(notification: StoredNotification): void {
    if (notification.isRead) return;
    this.items.update(list => list.map(n => n.id === notification.id ? { ...n, isRead: true } : n));
    this.unreadCount.update(c => Math.max(0, c - 1));
    this.http.post<void>(`${this.api}/${notification.id}/read`, {}).pipe(catchError(() => of(null))).subscribe();
  }

  markAllRead(): void {
    if (!this.unreadCount()) return;
    this.items.update(list => list.map(n => ({ ...n, isRead: true })));
    this.unreadCount.set(0);
    this.http.post<void>(`${this.api}/read-all`, {}).pipe(catchError(() => of(null))).subscribe();
  }

  title(n: StoredNotification): string {
    return translate(n.title, n.params);
  }

  body(n: StoredNotification): string {
    return translate(n.body, n.params);
  }
}
