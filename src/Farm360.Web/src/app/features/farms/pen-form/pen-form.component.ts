import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { PenService } from '../services/pen.service';

@Component({
  selector: 'app-pen-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './pen-form.component.html'
})
export class PenFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private penService = inject(PenService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  penForm!: FormGroup;
  isEditMode = false;
  penId: string = '';
  shedId: string = '';
  farmId: string = '';
  branchId: string = '';
  isSaving = false;

  ngOnInit(): void {
    this.branchId = this.route.snapshot.paramMap.get('branchId') || '';
    this.farmId = this.route.snapshot.paramMap.get('farmId') || '';
    this.shedId = this.route.snapshot.paramMap.get('shedId') || '';
    this.penId = this.route.snapshot.paramMap.get('penId') || '';
    
    this.isEditMode = !!this.penId;

    this.initForm();

    if (this.isEditMode) {
      this.loadPen();
    }
  }

  initForm(): void {
    this.penForm = this.fb.group({
      penNumber: ['', [Validators.required, Validators.maxLength(50)]],
      penName: ['', [Validators.required, Validators.maxLength(200)]],
      capacity: [10, [Validators.required, Validators.min(0)]],
      animalGroup: ['', Validators.maxLength(100)],
      notes: ['', Validators.maxLength(500)],
      status: [1]
    });
  }

  loadPen(): void {
    this.penService.getPenById(this.penId).subscribe(pen => {
      this.penForm.patchValue(pen);
      this.penForm.get('penNumber')?.disable();
    });
  }

  onSubmit(): void {
    if (this.penForm.invalid) return;

    this.isSaving = true;
    const formData = this.penForm.getRawValue();

    if (this.isEditMode) {
      this.penService.updatePen(this.penId, formData).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId, 'farms', this.farmId, 'sheds', this.shedId]);
        },
        error: (err) => {
          console.error(err);
          this.isSaving = false;
        }
      });
    } else {
      formData.shedId = this.shedId;
      this.penService.createPen(this.shedId, formData).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId, 'farms', this.farmId, 'sheds', this.shedId]);
        },
        error: (err) => {
          console.error(err);
          this.isSaving = false;
        }
      });
    }
  }
}
