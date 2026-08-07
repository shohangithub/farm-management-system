import { Component, OnInit, inject, signal, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ShedService } from '../services/shed.service';
import { CreateShedCommand, UpdateShedCommand } from '../models/shed.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-shed-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageHeaderComponent, MatSnackBarModule],
  templateUrl: './shed-form.component.html'
})
export class ShedFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly shedService = inject(ShedService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  shedForm!: FormGroup;
  isEditMode = signal<boolean>(false);
  branchId = signal<string>('');
  farmId = signal<string>('');
  shedId = signal<string | null>(null);
  isSubmitting = signal<boolean>(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.initForm();

    const branchId = this.route.snapshot.paramMap.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId');
    const farmId = this.route.snapshot.paramMap.get('farmId') || this.route.parent?.snapshot.paramMap.get('farmId');
    const shedId = this.route.snapshot.paramMap.get('shedId');

    if (branchId) this.branchId.set(branchId);
    if (farmId) this.farmId.set(farmId);

    if (shedId) {
      this.isEditMode.set(true);
      this.shedId.set(shedId);
      this.loadShed(shedId);
    }

    // Auto-uppercase shed number as the user types
    this.shedForm.get('shedNumber')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(value => {
        if (value && value !== value.toUpperCase()) {
          this.shedForm.get('shedNumber')?.setValue(value.toUpperCase(), { emitEvent: false });
        }
      });
  }

  initForm(): void {
    this.shedForm = this.fb.group({
      shedNumber: ['', [Validators.required, Validators.maxLength(50)]],
      shedName: ['', [Validators.required, Validators.maxLength(200)]],
      capacity: [null, [Validators.min(1)]],
      animalType: ['', Validators.maxLength(100)],
      floorType: ['', Validators.maxLength(100)],
      roofType: ['', Validators.maxLength(100)],
      hasVentilation: [false],
      hasWaterLine: [false],
      hasFeedLine: [false],
      status: ['Active']
    });
  }

  loadShed(id: string): void {
    this.shedService.getShedById(id).subscribe({
      next: (shed) => {
        this.shedForm.patchValue({
          shedNumber: shed.shedNumber,
          shedName: shed.shedName,
          capacity: shed.capacity,
          animalType: shed.animalType,
          floorType: shed.floorType,
          roofType: shed.roofType,
          hasVentilation: shed.hasVentilation,
          hasWaterLine: shed.hasWaterLine,
          hasFeedLine: shed.hasFeedLine,
          status: shed.status
        });
        // Shed number shouldn't change after creation
        this.shedForm.get('shedNumber')?.disable();
      },
      error: (err) => {
        this.error.set('Failed to load shed details.');
        console.error(err);
      }
    });
  }

  getError(field: string): string {
    const control = this.shedForm.get(field);
    if (!control || !control.touched || control.valid) return '';
    if (control.hasError('required')) return 'This field is required.';
    if (control.hasError('maxlength')) {
      const max = control.getError('maxlength').requiredLength;
      return `Maximum ${max} characters allowed.`;
    }
    if (control.hasError('min')) {
      return 'Value must be greater than zero.';
    }
    return 'Invalid value.';
  }

  onSubmit(): void {
    if (this.shedForm.invalid) {
      this.shedForm.markAllAsTouched();
      this.snackBar.open('Please fix the validation errors before submitting.', 'OK', {
        duration: 4000,
        panelClass: ['snack-error']
      });
      return;
    }

    this.isSubmitting.set(true);
    this.error.set(null);

    const formValue = this.shedForm.getRawValue();

    // Sanitize empty strings to null to prevent BadHttpRequestException during deserialization
    Object.keys(formValue).forEach(key => {
      if (formValue[key] === '') {
        formValue[key] = null;
      }
    });

    if (this.isEditMode() && this.shedId()) {
      const command: UpdateShedCommand = {
        id: this.shedId()!,
        farmId: this.farmId(),
        ...formValue
      };
      this.shedService.updateShed(this.shedId()!, command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Shed updated successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || 'Failed to update shed. Please check the inputs.';
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    } else {
      const command: CreateShedCommand = {
        farmId: this.farmId(),
        ...formValue
      };
      this.shedService.createShed(this.farmId(), command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Shed created successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || 'Failed to create shed. Please check the inputs.';
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    }
  }
}
