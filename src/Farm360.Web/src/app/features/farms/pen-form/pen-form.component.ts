import { Component, OnInit, inject, signal, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { PenService } from '../services/pen.service';
import { CreatePenCommand, UpdatePenCommand } from '../models/pen.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-pen-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageHeaderComponent, MatSnackBarModule],
  templateUrl: './pen-form.component.html'
})
export class PenFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly penService = inject(PenService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  penForm!: FormGroup;
  isEditMode = signal<boolean>(false);
  branchId = signal<string>('');
  farmId = signal<string>('');
  shedId = signal<string>('');
  penId = signal<string | null>(null);
  isSubmitting = signal<boolean>(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.initForm();

    let currentRoute: ActivatedRoute | null = this.route;
    let branchId = '';
    let farmId = '';
    let shedId = '';
    
    while (currentRoute) {
      if (!branchId) branchId = currentRoute.snapshot.paramMap.get('branchId') || '';
      if (!farmId) farmId = currentRoute.snapshot.paramMap.get('farmId') || '';
      if (!shedId) shedId = currentRoute.snapshot.paramMap.get('shedId') || '';
      currentRoute = currentRoute.parent;
    }

    const penId = this.route.snapshot.paramMap.get('penId');

    if (branchId) this.branchId.set(branchId);
    if (farmId) this.farmId.set(farmId);
    if (shedId) this.shedId.set(shedId);

    if (penId) {
      this.isEditMode.set(true);
      this.penId.set(penId);
      this.loadPen(penId);
    }

    // Auto-uppercase pen number as the user types
    this.penForm.get('penNumber')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(value => {
        if (value && value !== value.toUpperCase()) {
          this.penForm.get('penNumber')?.setValue(value.toUpperCase(), { emitEvent: false });
        }
      });
  }

  initForm(): void {
    this.penForm = this.fb.group({
      penNumber: ['', [Validators.required, Validators.maxLength(50)]],
      penName: ['', [Validators.required, Validators.maxLength(200)]],
      capacity: [null, [Validators.required, Validators.min(1)]],
      animalGroup: ['', Validators.maxLength(100)],
      notes: ['', Validators.maxLength(1000)],
      status: ['Active']
    });
  }

  loadPen(id: string): void {
    this.penService.getPenById(id).subscribe({
      next: (pen) => {
        this.penForm.patchValue({
          penNumber: pen.penNumber,
          penName: pen.penName,
          capacity: pen.capacity,
          animalGroup: pen.animalGroup,
          notes: pen.notes,
          status: pen.status
        });
        // Pen number shouldn't change after creation
        this.penForm.get('penNumber')?.disable();
      },
      error: (err) => {
        this.error.set('Failed to load pen details.');
        console.error(err);
      }
    });
  }

  getError(field: string): string {
    const control = this.penForm.get(field);
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
    if (this.penForm.invalid) {
      this.penForm.markAllAsTouched();
      this.snackBar.open('Please fix the validation errors before submitting.', 'OK', {
        duration: 4000,
        panelClass: ['snack-error']
      });
      return;
    }

    this.isSubmitting.set(true);
    this.error.set(null);

    const formValue = this.penForm.getRawValue();

    // Sanitize empty strings to null to prevent BadHttpRequestException during deserialization
    Object.keys(formValue).forEach(key => {
      if (formValue[key] === '') {
        formValue[key] = null;
      }
    });

    if (this.isEditMode() && this.penId()) {
      const command: UpdatePenCommand = {
        id: this.penId()!,
        ...formValue
      };
      this.penService.updatePen(this.penId()!, command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Pen updated successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || 'Failed to update pen. Please check the inputs.';
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    } else {
      const command: CreatePenCommand = {
        shedId: this.shedId(),
        ...formValue
      };
      this.penService.createPen(this.shedId(), command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Pen created successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || 'Failed to create pen. Please check the inputs.';
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    }
  }
}
