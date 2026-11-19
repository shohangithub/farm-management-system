import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MasterDataService } from '../../../shared/services/master-data.service';
import { MasterDataEntry, MasterDataType, CreateMasterDataCommand, UpdateMasterDataCommand } from '../../../shared/models/master-data.model';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { of, BehaviorSubject } from 'rxjs';

import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';

import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../core/utils/error-parser';

import { NotificationService } from '../../../shared/services/notification.service';
@Component({
  selector: 'app-master-data',
  standalone: true,
  imports: [CommonModule, FormsModule, DataTableComponent, MatIconModule, MatButtonModule, PageHeaderComponent, LoadingComponent, ErrorStateComponent],
  templateUrl: './master-data.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MasterDataComponent {
  private masterDataService = inject(MasterDataService);
  private dialog = inject(MatDialog);
  private notify = inject(NotificationService);
  readonly isSaving = signal(false);

  types = [
    { id: MasterDataType.Breed, name: 'Breed' },
    { id: MasterDataType.AnimalType, name: 'Animal Type' },
    { id: MasterDataType.FeedType, name: 'Feed Type' },
    { id: MasterDataType.MedicineType, name: 'Medicine Type' },
    { id: MasterDataType.VaccinationType, name: 'Vaccination Type' },
    { id: MasterDataType.Disease, name: 'Disease' },
    { id: MasterDataType.SupplierCategory, name: 'Supplier Category' },
    { id: MasterDataType.ExpenseCategory, name: 'Expense Category' },
    { id: MasterDataType.PaymentMethod, name: 'Payment Method' },
    { id: MasterDataType.MeasurementUnit, name: 'Measurement Unit' },
    { id: MasterDataType.Currency, name: 'Currency' },
    { id: MasterDataType.Language, name: 'Language' },
    { id: MasterDataType.Timezone, name: 'Timezone' },
    { id: MasterDataType.BusinessType, name: 'Business Type' }
  ];

  readonly selectedType = signal<MasterDataType>(MasterDataType.AnimalType);
  readonly isLoading = signal<boolean>(false);
  readonly loadError = signal<string | null>(null);

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    type: this.selectedType(),
    refresh: this.refreshTrigger()
  }));

  readonly entriesResult = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => { this.isLoading.set(true); this.loadError.set(null); }),
      switchMap(({ type }) => this.masterDataService.getByType(type, true).pipe(
        catchError(err => {
          this.loadError.set(parseApiError(err, 'Could not load master data.'));
          return of([] as MasterDataEntry[]);
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: [] as MasterDataEntry[] }
  );

  readonly entries = computed(() => this.entriesResult());

  // Form State
  isModalOpen = signal<boolean>(false);
  editingId = signal<string | null>(null);
  
  // Create a writable signal for the form data
  formData = signal<any>({
    name: '',
    code: '',
    description: '',
    displayOrder: 0,
    isActive: true
  });

  displayedColumns = ['code', 'name', 'order', 'status', 'actions'];

  columns: TableColumn[] = [
    { def: 'code', header: 'Code', cell: (row: MasterDataEntry) => `<span class="font-mono text-sm text-gray-600 dark:text-gray-400">${row.code}</span>`, isAction: false },
    { def: 'name', header: 'Name', cell: (row: MasterDataEntry) => `<span class="font-medium text-gray-900 dark:text-white">${row.name}</span>`, isAction: false },
    { def: 'order', header: 'Order', cell: (row: MasterDataEntry) => `<span class="text-gray-500">${row.displayOrder}</span>`, isAction: false },
    { def: 'status', header: 'Status', cell: (row: MasterDataEntry) => row.isActive ? '<span class="px-2 py-0.5 rounded-md text-[11px] font-bold uppercase tracking-wider bg-accent-50 text-accent-700 dark:bg-accent-900/30 dark:text-accent-400">Active</span>' : '<span class="px-2 py-0.5 rounded-md text-[11px] font-bold uppercase tracking-wider bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400">Inactive</span>', isAction: false },
    { def: 'actions', header: 'Actions', cell: () => '', isAction: true }
  ];

  selectType(type: MasterDataType): void {
    this.selectedType.set(type);
  }

  loadEntries(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  openCreateModal(): void {
    this.editingId.set(null);
    this.formData.set({ name: '', code: '', description: '', displayOrder: 0, isActive: true });
    this.isModalOpen.set(true);
  }

  openEditModal(entry: MasterDataEntry): void {
    this.editingId.set(entry.id);
    this.formData.set({ ...entry });
    this.isModalOpen.set(true);
  }

  closeModal(): void {
    this.isModalOpen.set(false);
  }

  updateFormField(field: string, value: any): void {
    this.formData.update(data => ({ ...data, [field]: value }));
  }

  saveEntry(): void {
    if (this.isSaving()) return;
    const data = this.formData();
    this.isSaving.set(true);
    const done = { next: () => { this.isSaving.set(false); this.closeModal(); this.loadEntries(); },
                   error: (err: unknown) => { this.isSaving.set(false); this.notify.error(parseApiError(err, 'Could not save the entry.')); } };
    const id = this.editingId();
    
    if (id) {
      const command: UpdateMasterDataCommand = {
        id: id,
        name: data.name,
        description: data.description,
        displayOrder: data.displayOrder,
        isActive: data.isActive
      };
      this.masterDataService.update(id, command, this.selectedType()).subscribe(done);
    } else {
      const command: CreateMasterDataCommand = {
        type: this.selectedType(),
        name: data.name,
        code: data.code,
        description: data.description,
        displayOrder: data.displayOrder
      };
      this.masterDataService.create(command).subscribe(done);
    }
  }

  deleteEntry(entry: MasterDataEntry): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      data: {
        title: 'Delete Master Data',
        message: `Are you sure you want to delete ${entry.name}? This action cannot be undone.`,
        confirmButtonText: 'Delete',
        cancelButtonText: 'Cancel',
        isDestructive: true
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.masterDataService.delete(entry.id, this.selectedType()).subscribe({
          next: () => this.loadEntries(),
          error: err => this.notify.error(parseApiError(err, 'Could not delete the entry.'))
        });
      }
    });
  }
}
