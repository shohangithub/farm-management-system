import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { HealthService } from '../../services/health.service';
import { IncidentSeverity } from '../../models/health.models';

@Component({
  selector: 'app-report-incident',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './report-incident.component.html',
  styleUrls: ['./report-incident.component.scss']
})
export class ReportIncidentComponent {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private router = inject(Router);

  incidentForm: FormGroup;
  isSubmitting = false;
  error = '';
  
  // Hardcoded MVP farm ID
  private farmId = '11111111-1111-1111-1111-111111111111';

  constructor() {
    this.incidentForm = this.fb.group({
      diseaseName: ['', [Validators.required, Validators.maxLength(150)]],
      severity: [IncidentSeverity.Moderate, Validators.required],
      incidentDate: [new Date().toISOString().split('T')[0], Validators.required],
      symptoms: ['', [Validators.required, Validators.maxLength(1000)]],
      affectedAnimalCount: [1, [Validators.required, Validators.min(1)]],
      notes: ['', Validators.maxLength(1000)]
    });
  }

  onSubmit(): void {
    if (this.incidentForm.invalid) {
      this.incidentForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.error = '';
    
    const formValue = this.incidentForm.value;
    const request = {
      farmId: this.farmId,
      ...formValue
    };

    this.healthService.reportIncident(request).subscribe({
      next: (res) => {
        this.isSubmitting = false;
        // Navigation could go to a detail page, for now just back to health dashboard
        this.router.navigate(['/health/vaccinations']);
      },
      error: (err) => {
        console.error(err);
        this.error = 'Failed to report incident. Please try again.';
        this.isSubmitting = false;
      }
    });
  }
}
