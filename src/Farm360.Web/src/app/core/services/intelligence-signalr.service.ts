import { Injectable, Injector, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';
import * as signalR from '@microsoft/signalr';
import { ActionableInsight } from '../../features/livestock/components/intelligence-panel/intelligence-panel.component';
import { NotificationService } from '../../shared/services/notification.service';

@Injectable({
  providedIn: 'root'
})
export class IntelligenceSignalRService {
  private readonly injector = inject(Injector);
  private hubConnection: signalR.HubConnection | null = null;
  public readonly latestInsight = signal<ActionableInsight | null>(null);
  public readonly notifications = signal<ActionableInsight[]>([]);
  public readonly unreadCount = signal<number>(0);

  constructor(private notify: NotificationService) {}

  public startConnection(): void {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/farm-notifications', {
        // The HTTP interceptor doesn't cover SignalR, so the sign-in token is passed here: as a
        // header for negotiate and as ?access_token= for the WebSocket, which the API reads.
        // Looked up lazily to avoid a circular dependency with AuthService.
        accessTokenFactory: () => this.injector.get(AuthService).accessToken ?? ''
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .catch(err => console.error('Error while starting SignalR connection: ' + err));

    this.addIntelligenceInsightListener();
  }

  private addIntelligenceInsightListener(): void {
    if (!this.hubConnection) return;
    
    this.hubConnection.on('NewIntelligenceInsight', (data: ActionableInsight) => {
      this.latestInsight.set(data);
      this.notifications.update(n => [data, ...n]);
      this.unreadCount.update(c => c + 1);
      this.showToast(data);
    });
  }

  public markAllAsRead(): void {
    this.unreadCount.set(0);
  }

  private showToast(insight: ActionableInsight): void {
    const text = `${insight.title}: ${insight.message}`;
    switch (insight.severity) {
      case 'Critical': this.notify.error(text); break;
      case 'Warning': this.notify.warning(text); break;
      case 'Success': this.notify.success(text); break;
      default: this.notify.info(text);
    }
  }

  public stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }
}
