import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ShedService } from '../services/shed.service';

@Component({
  selector: 'app-shed-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './shed-form.component.html'
})
export class ShedFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private shedService = inject(ShedService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  shedForm!: FormGroup;
  isEditMode = false;
  shedId: string = '';
  farmId: string = '';
  branchId: string = '';
  isSaving = false;
  errorMessage = '';

  ngOnInit(): void {
    this.branchId = this.route.snapshot.paramMap.get('branchId') || '';
    this.farmId = this.route.snapshot.paramMap.get('farmId') || '';
    this.shedId = this.route.snapshot.paramMap.get('shedId') || '';
    this.isEditMode = !!this.shedId;

    this.initForm();

    if (this.isEditMode) {
      this.loadShed();
    }
  }

  initForm(): void {
    this.shedForm = this.fb.group({
      shedNumber: ['', [Validators.required, Validators.maxLength(50)]],
      shedName: ['', [Validators.required, Validators.maxLength(200)]],
      capacity: [null, Validators.min(0)],
      animalType: ['', Validators.maxLength(100)],
      floorType: ['', Validators.maxLength(100)],
      roofType: ['', Validators.maxLength(100)],
      hasVentilation: [false],
      hasWaterLine: [false],
      hasFeedLine: [false],
      status: [1]
    });
  }

  loadShed(): void {
    this.shedService.getShedById(this.shedId).subscribe(shed => {
      this.shedForm.patchValue(shed);
      this.shedForm.get('shedNumber')?.disable();
    });
  }

  onSubmit(): void {
    if (this.shedForm.invalid) return;

    this.isSaving = true;
    this.errorMessage = '';
    const formData = this.shedForm.getRawValue();

    if (this.isEditMode) {
      this.shedService.updateShed(this.shedId, formData).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId, 'farms', this.farmId]);
        },
        error: (err) => {
          console.error(err);
          this.errorMessage = err.error?.detail || err.error?.title || 'An unexpected error occurred.';
          this.isSaving = false;
        }
      });
    } else {
      formData.farmId = this.farmId;
      this.shedService.createShed(this.farmId, formData).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId, 'farms', this.farmId]);
        },
        error: (err) => {
          console.error(err);
          this.errorMessage = err.error?.detail || err.error?.title || 'An unexpected error occurred.';
          this.isSaving = false;
        }
      });
    }
  }
}
