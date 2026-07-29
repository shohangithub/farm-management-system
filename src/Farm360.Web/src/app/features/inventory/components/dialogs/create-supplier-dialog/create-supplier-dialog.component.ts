import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable } from 'rxjs';
import { InventoryService } from '../../../services/inventory.service';
import { Supplier } from '../../../models/inventory.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-create-supplier-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-2xl overflow-hidden max-w-xl">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2">
          <div class="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
            <mat-icon class="!w-5 !h-5 !text-[20px]">local_shipping</mat-icon>
          </div>
          <span>{{ isEdit ? 'Edit Supplier' : 'Add New Supplier' }}</span>
        </h2>
        <button mat-icon-button (click)="dialogRef.close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <div class="p-6">
        @if (error()) {
          <div class="mb-4 p-3 rounded-xl bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 text-xs border border-red-200 dark:border-red-800 font-medium">
            {{ error() }}
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col gap-4">
          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Supplier / Vendor Name</mat-label>
            <input matInput formControlName="name" placeholder="e.g. ACI Animal Health Ltd." required />
            <mat-error>Supplier name is required</mat-error>
          </mat-form-field>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Contact Person</mat-label>
              <input matInput formControlName="contactPerson" placeholder="e.g. Engr. Karim" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Phone Number</mat-label>
              <input matInput formControlName="phone" placeholder="+8801700000000" />
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Email Address</mat-label>
            <input matInput type="email" formControlName="email" placeholder="sales@supplier.com" />
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Address</mat-label>
            <textarea matInput formControlName="address" rows="2" placeholder="Tejgaon Industrial Area, Dhaka"></textarea>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Notes</mat-label>
            <textarea matInput formControlName="notes" rows="2" placeholder="Payment terms or discount agreement"></textarea>
          </mat-form-field>
        </form>
      </div>

      <!-- Actions -->
      <div class="px-6 py-4 bg-gray-50/50 dark:bg-gray-900/30 border-t border-gray-100 dark:border-gray-800 flex justify-end gap-2">
        <button class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors" [disabled]="isSubmitting()" (click)="dialogRef.close()">
          Cancel
        </button>
        <button class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50" [disabled]="form.invalid || isSubmitting()" (click)="onSubmit()">
          <mat-spinner *ngIf="isSubmitting()" diameter="16"></mat-spinner>
          <span>{{ isEdit ? 'Update Supplier' : 'Save Supplier' }}</span>
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateSupplierDialogComponent {
  readonly dialogRef = inject(MatDialogRef<CreateSupplierDialogComponent>);
  readonly data = inject<Supplier | null>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly inventoryService = inject(InventoryService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isSubmitting = signal(false);
  readonly error = signal('');
  readonly isEdit = !!this.data;

  readonly form = this.fb.group({
    name: [this.data?.name || '', [Validators.required, Validators.maxLength(200)]],
    contactPerson: [this.data?.contactPerson || ''],
    phone: [this.data?.phone || ''],
    email: [this.data?.email || ''],
    address: [this.data?.address || ''],
    notes: [this.data?.notes || '']
  });

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set('');

    const formVal = this.form.getRawValue();
    const val = {
      ...formVal,
      contactPerson: formVal.contactPerson ? formVal.contactPerson : null,
      phone: formVal.phone ? formVal.phone : null,
      email: formVal.email ? formVal.email : null,
      address: formVal.address ? formVal.address : null,
      notes: formVal.notes ? formVal.notes : null
    };

    const request$: Observable<any> = this.isEdit && this.data
      ? this.inventoryService.updateSupplier(this.data.id, val as any)
      : this.inventoryService.createSupplier(val as any);

    request$.subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open(`Supplier ${this.isEdit ? 'updated' : 'created'} successfully.`, 'OK', { duration: 4000 });
        this.dialogRef.close(true);
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
