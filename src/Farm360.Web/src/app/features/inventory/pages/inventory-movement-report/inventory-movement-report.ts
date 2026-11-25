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
import { BehaviorSubject, switchMap, of, catchError, startWith } from 'rxjs';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

import { toLocalIsoDate } from '../../../../shared/utils/local-date';
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
    EmptyStateComponent,
    ErrorStateComponent,
    TranslatePipe
  ],
  templateUrl: './inventory-movement-report.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InventoryMovementReport implements OnInit {
  private inventoryService = inject(InventoryService);
  private workingContextService = inject(WorkingContextService);
  private fb = inject(FormBuilder);
  private pdfService = inject(PdfExportService);
  private readonly i18n = inject(I18nService);

  readonly isExporting = signal(false);
  readonly loadError = signal<string | null>(null);
  filterForm!: FormGroup;

  private filterSubmit$ = new BehaviorSubject<{ startDate: string, endDate: string } | null>(null);

  private readonly reportData$ = this.filterSubmit$.pipe(
    switchMap(filters => {
      const farmId = this.workingContextService.currentFarmValue?.id;
      if (!filters || !farmId) return of(null);
      this.loadError.set(null);
      return this.inventoryService.getMovementReport(farmId, filters.startDate, filters.endDate)
        .pipe(
          catchError(err => {
            this.loadError.set(parseApiError(err, this.i18n.t('Could not load the movement report.')));
            return of(null);
          }),
          startWith(undefined)
        );
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
      startDate: toLocalIsoDate(start),
      endDate: toLocalIsoDate(end)
    });
  }

  async exportPdf(): Promise<void> {
    const element = document.getElementById('reportSheet');
    if (!element) return;

    this.isExporting.set(true);
    try {
      const farmName = this.workingContextService.currentFarmValue?.name || this.i18n.t('All Units');
      const start = this.filterForm.value.start ? new Date(this.filterForm.value.start).toLocaleDateString('en-GB') : '';
      const end = this.filterForm.value.end ? new Date(this.filterForm.value.end).toLocaleDateString('en-GB') : '';
      await this.pdfService.exportElement(element, {
        filename: `Inventory_Movement_Report_${toLocalIsoDate()}`,
        orientation: 'landscape',
        header: {
          title: this.i18n.t('Inventory Movement & Consumption Report'),
          subtitle: this.i18n.t('Track stock flow, consumption, and closing balances'),
          farmName,
          dateRange: this.i18n.t('Period: {start} - {end}', { start, end }),
          currency: 'BDT (৳)'
        },
        showSignatures: true
      });
    } finally {
      this.isExporting.set(false);
    }
  }
}
