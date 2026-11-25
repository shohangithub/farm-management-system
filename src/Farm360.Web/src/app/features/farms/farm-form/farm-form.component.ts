import { Component, OnInit, inject, signal, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { FarmService } from '../services/farm.service';
import { CreateFarmCommand, UpdateFarmCommand } from '../models/farm.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NotificationService } from '../../../shared/services/notification.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

import { HasUnsavedChanges } from '../../../shared/utils/unsaved-changes.guard';

@Component({
  selector: 'app-farm-form',
  standalone: true,
  imports: [TranslatePipe, CommonModule, ReactiveFormsModule, RouterModule, PageHeaderComponent, MatSnackBarModule],
  templateUrl: './farm-form.component.html'
})
export class FarmFormComponent implements OnInit, HasUnsavedChanges {
  private readonly i18n = inject(I18nService);
  /** Set once the form is saved so the unsaved-changes guard lets navigation through. */
  private saved = false;

  hasUnsavedChanges(): boolean {
    return !!this.farmForm?.dirty && !this.saved;
  }

  private readonly fb = inject(FormBuilder);
  private readonly farmService = inject(FarmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);

  farmForm!: FormGroup;
  isEditMode = signal<boolean>(false);
  branchId = signal<string>('');
  farmId = signal<string | null>(null);
  isSubmitting = signal<boolean>(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.initForm();

    const branchId = this.route.snapshot.paramMap.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId');
    const farmId = this.route.snapshot.paramMap.get('farmId');

    if (branchId) {
      this.branchId.set(branchId);
    }

    if (farmId) {
      this.isEditMode.set(true);
      this.farmId.set(farmId);
      this.loadFarm(farmId);
    }

    // Auto-uppercase farm code as the user types
    this.farmForm.get('farmCode')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(value => {
        if (value && value !== value.toUpperCase()) {
          this.farmForm.get('farmCode')?.setValue(value.toUpperCase(), { emitEvent: false });
        }
      });
  }

  initForm(): void {
    this.farmForm = this.fb.group({
      farmCode: ['', [Validators.required, Validators.maxLength(50)]],
      farmName: ['', [Validators.required, Validators.maxLength(200)]],
      type: ['Dairy', Validators.required],
      farmSize: [null, Validators.min(0)],
      landArea: [null, Validators.min(0)],
      latitude: [null, [Validators.min(-90), Validators.max(90)]],
      longitude: [null, [Validators.min(-180), Validators.max(180)]],
      mapPolygon: [''],
      capacity: [null, Validators.min(0)],
      status: ['Active'],
      description: ['', Validators.maxLength(1000)]
    });
  }

  loadFarm(id: string): void {
    this.farmService.getFarmById(id).subscribe({
      next: (farm) => {
        this.farmForm.patchValue({
          farmCode: farm.farmCode,
          farmName: farm.farmName,
          type: farm.type,
          farmSize: farm.farmSize,
          landArea: farm.landArea,
          latitude: farm.latitude,
          longitude: farm.longitude,
          mapPolygon: farm.mapPolygon,
          capacity: farm.capacity,
          status: farm.status,
          description: farm.description
        });
        // Farm code shouldn't change after creation
        this.farmForm.get('farmCode')?.disable();
      },
      error: (err) => {
        this.error.set(this.i18n.t('Failed to load farm details.'));
        console.error(err);
      }
    });
  }

  getError(field: string): string {
    const control = this.farmForm.get(field);
    if (!control || !control.touched || control.valid) return '';
    if (control.hasError('required')) return 'This field is required.';
    if (control.hasError('maxlength')) {
      const max = control.getError('maxlength').requiredLength;
      return `Maximum ${max} characters allowed.`;
    }
    if (control.hasError('min')) {
      return this.i18n.t('Value must be at least {min}.', { min: control.getError('min').min });
    }
    if (control.hasError('max')) {
      return this.i18n.t('Value must be at most {max}.', { max: control.getError('max').max });
    }
    return 'Invalid value.';
  }

  onSubmit(): void {
    if (this.farmForm.invalid) {
      this.farmForm.markAllAsTouched();
      this.notify.error(this.i18n.t('Please fix the validation errors before submitting.'));
      return;
    }

    this.isSubmitting.set(true);
    this.error.set(null);

    const formValue = this.farmForm.getRawValue();

    // Sanitize empty strings to null to prevent BadHttpRequestException during deserialization
    Object.keys(formValue).forEach(key => {
      if (formValue[key] === '') {
        formValue[key] = null;
      }
    });

    if (this.isEditMode() && this.farmId()) {
      const command: UpdateFarmCommand = {
        id: this.farmId()!,
        branchId: this.branchId(),
        ...formValue
      };
      this.farmService.updateFarm(this.farmId()!, command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.notify.success(this.i18n.t('Farm updated successfully!'));
          this.saved = true;
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || this.i18n.t('Failed to update farm. Please check the inputs.');
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    } else {
      const command: CreateFarmCommand = {
        branchId: this.branchId(),
        ...formValue
      };
      this.farmService.createFarm(this.branchId(), command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.notify.success(this.i18n.t('Farm created successfully!'));
          this.saved = true;
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || this.i18n.t('Failed to create farm. Please check the inputs.');
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    }
  }
}
