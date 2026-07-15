import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { OrganizationService } from '../services/organization.service';
import { CreateOrganizationCommand, UpdateOrganizationCommand } from '../models/organization.model';

@Component({
  selector: 'app-organization-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './organization-form.html',
  styleUrls: ['./organization-form.scss']
})
export class OrganizationFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly organizationService = inject(OrganizationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  orgForm!: FormGroup;
  isEditMode = signal<boolean>(false);
  orgId = signal<string | null>(null);
  isSubmitting = signal<boolean>(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.initForm();
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode.set(true);
      this.orgId.set(id);
      this.loadOrganization(id);
    }
  }

  initForm(): void {
    this.orgForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      businessType: [1, Validators.required],
      contactEmail: ['', [Validators.required, Validators.email, Validators.maxLength(150)]],
      contactPhone: ['', Validators.maxLength(30)],
      currencyCode: ['USD', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
      timeZoneId: ['UTC', Validators.required],
      languageCode: ['en-US', Validators.required],
      businessRegistrationNumber: ['', Validators.maxLength(100)],
      tradeLicenseNumber: ['', Validators.maxLength(100)],
      taxIdentificationNumber: ['', Validators.maxLength(100)],
      street: [''],
      city: [''],
      state: [''],
      country: [''],
      zipCode: ['']
    });
  }

  loadOrganization(id: string): void {
    this.organizationService.getOrganizationById(id).subscribe({
      next: (org) => {
        this.orgForm.patchValue({
          name: org.name,
          businessType: org.businessType,
          contactEmail: org.contactEmail,
          contactPhone: org.contactPhone,
          currencyCode: org.currencyCode,
          timeZoneId: org.timeZoneId,
          languageCode: org.languageCode,
          businessRegistrationNumber: org.businessRegistrationNumber,
          tradeLicenseNumber: org.tradeLicenseNumber,
          taxIdentificationNumber: org.taxIdentificationNumber,
          street: org.address?.street,
          city: org.address?.city,
          state: org.address?.state,
          country: org.address?.country,
          zipCode: org.address?.zipCode
        });
      },
      error: (err) => {
        this.error.set('Failed to load organization details.');
        console.error(err);
      }
    });
  }

  onSubmit(): void {
    if (this.orgForm.invalid) {
      this.orgForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.error.set(null);

    const formValue = this.orgForm.value;

    if (this.isEditMode() && this.orgId()) {
      const command: UpdateOrganizationCommand = {
        id: this.orgId()!,
        ...formValue
      };
      this.organizationService.updateOrganization(this.orgId()!, command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.router.navigate(['/organizations']);
        },
        error: (err) => {
          this.error.set('Failed to update organization. Please check the inputs.');
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    } else {
      const command: CreateOrganizationCommand = { ...formValue };
      this.organizationService.createOrganization(command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.router.navigate(['/organizations']);
        },
        error: (err) => {
          this.error.set('Failed to create organization. Please check the inputs.');
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    }
  }
}
