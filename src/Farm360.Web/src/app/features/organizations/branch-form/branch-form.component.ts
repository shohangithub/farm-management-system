import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Subject, takeUntil } from 'rxjs';
import { BranchService } from '../services/branch.service';
import { CreateBranchCommand, UpdateBranchCommand } from '../models/branch.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-branch-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageHeaderComponent, MatSnackBarModule],
  templateUrl: './branch-form.html',
  styleUrls: ['./branch-form.scss']
})
export class BranchFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly branchService = inject(BranchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroy$ = new Subject<void>();

  branchForm!: FormGroup;
  isEditMode = signal<boolean>(false);
  orgId = signal<string>('');
  branchId = signal<string | null>(null);
  isSubmitting = signal<boolean>(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.initForm();

    const orgId = this.route.snapshot.paramMap.get('orgId');
    const branchId = this.route.snapshot.paramMap.get('branchId');

    if (orgId) {
      this.orgId.set(orgId);
    }

    if (branchId) {
      this.isEditMode.set(true);
      this.branchId.set(branchId);
      this.loadBranch(branchId);
    }

    // Auto-uppercase branch code as the user types
    this.branchForm.get('branchCode')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(value => {
        if (value && value !== value.toUpperCase()) {
          this.branchForm.get('branchCode')?.setValue(value.toUpperCase(), { emitEvent: false });
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  initForm(): void {
    this.branchForm = this.fb.group({
      branchCode: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      contactEmail: ['', [Validators.required, Validators.email, Validators.maxLength(150)]],
      contactPhone: ['', Validators.maxLength(30)],
      isHeadOffice: [false],
      status: [1],
      street: [''],
      city: [''],
      state: [''],
      country: [''],
      zipCode: [''],
      latitude: [null],
      longitude: [null],
      workingHours: [''],
      holidayCalendar: ['']
    });
  }

  loadBranch(id: string): void {
    this.branchService.getBranchById(id).subscribe({
      next: (branch) => {
        this.branchForm.patchValue({
          branchCode: branch.branchCode,
          name: branch.name,
          contactEmail: branch.contactEmail,
          contactPhone: branch.contactPhone,
          isHeadOffice: branch.isHeadOffice,
          status: branch.status,
          street: branch.address?.street,
          city: branch.address?.city,
          state: branch.address?.state,
          country: branch.address?.country,
          zipCode: branch.address?.zipCode,
          latitude: branch.latitude,
          longitude: branch.longitude,
          workingHours: branch.workingHours,
          holidayCalendar: branch.holidayCalendar
        });
        // Branch code shouldn't change after creation
        this.branchForm.get('branchCode')?.disable();
      },
      error: (err) => {
        this.error.set('Failed to load branch details.');
        console.error(err);
      }
    });
  }

  getError(field: string): string {
    const control = this.branchForm.get(field);
    if (!control || !control.touched || control.valid) return '';
    if (control.hasError('required')) return 'This field is required.';
    if (control.hasError('email')) return 'Please enter a valid email address.';
    if (control.hasError('maxlength')) {
      const max = control.getError('maxlength').requiredLength;
      return `Maximum ${max} characters allowed.`;
    }
    return 'Invalid value.';
  }

  onSubmit(): void {
    if (this.branchForm.invalid) {
      this.branchForm.markAllAsTouched();
      this.snackBar.open('Please fix the validation errors before submitting.', 'OK', {
        duration: 4000,
        panelClass: ['snack-error']
      });
      return;
    }

    this.isSubmitting.set(true);
    this.error.set(null);

    const formValue = this.branchForm.getRawValue();

    if (this.isEditMode() && this.branchId()) {
      const command: UpdateBranchCommand = {
        id: this.branchId()!,
        organizationId: this.orgId(),
        ...formValue
      };
      this.branchService.updateBranch(this.branchId()!, command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Branch updated successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.router.navigate(['/organizations', this.orgId(), 'branches']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || 'Failed to update branch. Please check the inputs.';
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    } else {
      const command: CreateBranchCommand = {
        organizationId: this.orgId(),
        ...formValue
      };
      this.branchService.createBranch(this.orgId(), command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Branch created successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.router.navigate(['/organizations', this.orgId(), 'branches']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || 'Failed to create branch. Please check the inputs.';
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    }
  }
}
