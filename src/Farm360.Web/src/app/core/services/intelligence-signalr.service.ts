import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { ActionableInsight } from '../../features/livestock/components/intelligence-panel/intelligence-panel.component';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable({
  providedIn: 'root'
})
export class IntelligenceSignalRService {
  private hubConnection: signalR.HubConnection | null = null;
  public readonly latestInsight = signal<ActionableInsight | null>(null);
  public readonly notifications = signal<ActionableInsight[]>([]);
  public readonly unreadCount = signal<number>(0);

  constructor(private snackBar: MatSnackBar) {}

  public startConnection(): void {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/farm-notifications', {
        // Typically, we pass the JWT token here. For this phase, we assume cookie auth or interceptor works
        // if interceptor doesn't apply to WebSockets, we'd add accessTokenFactory
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('Intelligence SignalR Connection started'))
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
    let panelClass = 'bg-blue-500';
    if (insight.severity === 'Warning') panelClass = 'bg-amber-500';
    if (insight.severity === 'Critical') panelClass = 'bg-red-500';
    if (insight.severity === 'Success') panelClass = 'bg-emerald-500';

    this.snackBar.open(`💡 ${insight.title}: ${insight.message}`, 'View', {
      duration: 10000,
      horizontalPosition: 'right',
      verticalPosition: 'top',
      panelClass: [panelClass, 'text-white']
    });
  }

  public stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }
}
