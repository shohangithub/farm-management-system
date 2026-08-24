import { Component, ChangeDetectionStrategy, inject, signal, computed, Input, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { toSignal } from '@angular/core/rxjs-interop';
import { BehaviorSubject, switchMap, of, catchError } from 'rxjs';
import { InventoryService } from '../../services/inventory.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-expiring-items-panel',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    DatePipe,
    LoadingComponent,
    EmptyStateComponent
  ],
  templateUrl: './expiring-items-panel.html',
  styleUrl: './expiring-items-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExpiringItemsPanel implements OnInit {
  private inventoryService = inject(InventoryService);
  
  @Input({ required: true }) farmId!: string;
  @Input() daysThreshold: number = 30;

  private refreshTrigger$ = new BehaviorSubject<void>(undefined);

  private readonly data$ = this.refreshTrigger$.pipe(
    switchMap(() => {
      if (!this.farmId) return of([]);
      return this.inventoryService.getExpiringItems(this.farmId, this.daysThreshold).pipe(catchError(() => of([])));
    })
  );

  private readonly itemsData = toSignal(this.data$);

  readonly items = computed(() => this.itemsData() ?? []);
  readonly isLoading = computed(() => this.itemsData() === undefined);

  ngOnInit(): void {
    if (this.farmId) {
      this.refreshTrigger$.next();
    }
  }

  refresh(): void {
    this.refreshTrigger$.next();
  }
}
