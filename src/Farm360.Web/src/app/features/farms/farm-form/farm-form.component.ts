import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Subject, takeUntil } from 'rxjs';
import { FarmService } from '../services/farm.service';
import { CreateFarmCommand, UpdateFarmCommand } from '../models/farm.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-farm-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageHeaderComponent, MatSnackBarModule],
  templateUrl: './farm-form.component.html'
})
export class FarmFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly farmService = inject(FarmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroy$ = new Subject<void>();

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
      .pipe(takeUntil(this.destroy$))
      .subscribe(value => {
        if (value && value !== value.toUpperCase()) {
          this.farmForm.get('farmCode')?.setValue(value.toUpperCase(), { emitEvent: false });
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  initForm(): void {
    this.farmForm = this.fb.group({
      farmCode: ['', [Validators.required, Validators.maxLength(50)]],
      farmName: ['', [Validators.required, Validators.maxLength(200)]],
      type: [1, Validators.required],
      farmSize: [null],
      landArea: [null],
      latitude: [null],
      longitude: [null],
      mapPolygon: [''],
      capacity: [null, Validators.min(0)],
      status: [1],
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
        this.error.set('Failed to load farm details.');
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
      return 'Value must be zero or greater.';
    }
    return 'Invalid value.';
  }

  onSubmit(): void {
    if (this.farmForm.invalid) {
      this.farmForm.markAllAsTouched();
      this.snackBar.open('Please fix the validation errors before submitting.', 'OK', {
        duration: 4000,
        panelClass: ['snack-error']
      });
      return;
    }

    this.isSubmitting.set(true);
    this.error.set(null);

    const formValue = this.farmForm.getRawValue();

    if (this.isEditMode() && this.farmId()) {
      const command: UpdateFarmCommand = {
        id: this.farmId()!,
        branchId: this.branchId(),
        ...formValue
      };
      this.farmService.updateFarm(this.farmId()!, command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Farm updated successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || 'Failed to update farm. Please check the inputs.';
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
          this.snackBar.open('Farm created successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || 'Failed to create farm. Please check the inputs.';
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    }
  }
}
