import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { BranchService } from '../services/branch.service';
import { CreateBranchCommand, UpdateBranchCommand } from '../models/branch.model';

@Component({
  selector: 'app-branch-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './branch-form.html',
  styleUrls: ['./branch-form.scss']
})
export class BranchFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly branchService = inject(BranchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

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
  }

  initForm(): void {
    this.branchForm = this.fb.group({
      branchCode: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      contactEmail: ['', [Validators.required, Validators.email, Validators.maxLength(150)]],
      contactPhone: ['', Validators.maxLength(30)],
      isHeadOffice: [false],
      status: [1], // Only for edit mode usually, but added here
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
        this.branchForm.get('branchCode')?.disable(); // Usually code shouldn't change
      },
      error: (err) => {
        this.error.set('Failed to load branch details.');
        console.error(err);
      }
    });
  }

  onSubmit(): void {
    if (this.branchForm.invalid) {
      this.branchForm.markAllAsTouched();
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
          this.router.navigate(['/organizations', this.orgId(), 'branches']);
        },
        error: (err) => {
          this.error.set('Failed to update branch. Please check the inputs.');
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
          this.router.navigate(['/organizations', this.orgId(), 'branches']);
        },
        error: (err) => {
          this.error.set('Failed to create branch. Please check the inputs.');
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    }
  }
}
