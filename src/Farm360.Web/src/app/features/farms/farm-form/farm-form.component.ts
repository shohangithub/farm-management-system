import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { FarmService } from '../services/farm.service';

@Component({
  selector: 'app-farm-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './farm-form.component.html'
})
export class FarmFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private farmService = inject(FarmService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  farmForm!: FormGroup;
  isEditMode = false;
  farmId: string = '';
  branchId: string = '';
  isSaving = false;

  ngOnInit(): void {
    this.branchId = this.route.snapshot.paramMap.get('branchId') || '';
    this.farmId = this.route.snapshot.paramMap.get('farmId') || '';
    this.isEditMode = !!this.farmId;

    this.initForm();

    if (this.isEditMode) {
      this.loadFarm();
    }
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

  loadFarm(): void {
    this.farmService.getFarmById(this.farmId).subscribe(farm => {
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
      // Disable farmCode on edit
      this.farmForm.get('farmCode')?.disable();
    });
  }

  onSubmit(): void {
    if (this.farmForm.invalid) return;

    this.isSaving = true;
    const formData = this.farmForm.getRawValue();

    if (this.isEditMode) {
      this.farmService.updateFarm(this.farmId, formData).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId, 'farms']);
        },
        error: (err) => {
          console.error(err);
          this.isSaving = false;
        }
      });
    } else {
      formData.branchId = this.branchId;
      this.farmService.createFarm(this.branchId, formData).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId, 'farms']);
        },
        error: (err) => {
          console.error(err);
          this.isSaving = false;
        }
      });
    }
  }
}
