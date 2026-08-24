import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { toSignal } from '@angular/core/rxjs-interop';
import { BehaviorSubject, switchMap, of, catchError } from 'rxjs';
import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-inventory-movement-report',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent
  ],
  templateUrl: './inventory-movement-report.html',
  styleUrl: './inventory-movement-report.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InventoryMovementReport implements OnInit {
  private inventoryService = inject(InventoryService);
  private workingContextService = inject(WorkingContextService);
  private fb = inject(FormBuilder);

  filterForm!: FormGroup;

  private filterSubmit$ = new BehaviorSubject<{ startDate: string, endDate: string } | null>(null);

  private readonly reportData$ = this.filterSubmit$.pipe(
    switchMap(filters => {
      const farmId = this.workingContextService.currentFarmValue?.id;
      if (!filters || !farmId) return of(null);
      return this.inventoryService.getMovementReport(farmId, filters.startDate, filters.endDate)
        .pipe(catchError(() => of(null)));
    })
  );

  private readonly reportData = toSignal(this.reportData$);

  readonly items = computed(() => this.reportData()?.items ?? []);
  readonly isLoading = computed(() => this.filterSubmit$.value !== null && this.reportData() === undefined);

  readonly displayedColumns = [
    'itemName', 'category', 'openingStock', 'received', 
    'consumed', 'writtenOff', 'closingStock', 'closingValue'
  ];

  ngOnInit(): void {
    const today = new Date();
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);

    this.filterForm = this.fb.group({
      start: [firstDay],
      end: [today]
    });

    this.onGenerate();
  }

  onGenerate(): void {
    if (this.filterForm.invalid) return;
    const start: Date = this.filterForm.value.start;
    const end: Date = this.filterForm.value.end;

    if (!start || !end) return;

    this.filterSubmit$.next({
      startDate: start.toISOString().split('T')[0],
      endDate: end.toISOString().split('T')[0]
    });
  }
}
