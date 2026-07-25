import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { OrganizationService } from '../services/organization.service';
import { CreateOrganizationCommand, UpdateOrganizationCommand } from '../models/organization.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-organization-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, MatIconModule, PageHeaderComponent],
  templateUrl: './organization-form.html',
  styleUrls: ['./organization-form.scss']
})
export class OrganizationFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly organizationService = inject(OrganizationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  orgForm!: FormGroup;
  isEditMode = signal<boolean>(false);
  orgId = signal<string | null>(null);
  isSubmitting = signal<boolean>(false);
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  // Logo upload state
  selectedLogoFile = signal<File | null>(null);
  logoPreviewUrl = signal<string | null>(null);

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
      currencyCode: ['BDT', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
      timeZoneId: ['Asia/Dhaka', Validators.required],
      languageCode: ['en', Validators.required],
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
          businessType: +org.businessType, // Ensure integer
          contactEmail: org.contactEmail,
          contactPhone: org.contactPhone || '',
          currencyCode: org.currencyCode,
          timeZoneId: org.timeZoneId,
          languageCode: org.languageCode,
          businessRegistrationNumber: org.businessRegistrationNumber || '',
          tradeLicenseNumber: org.tradeLicenseNumber || '',
          taxIdentificationNumber: org.taxIdentificationNumber || '',
          street: org.address?.street || '',
          city: org.address?.city || '',
          state: org.address?.state || '',
          country: org.address?.country || '',
          zipCode: org.address?.zipCode || ''
        });

        if (org.logoUrl) {
          // If LogoUrl is relative, it will map to backend via proxy or absolute path.
          // Assuming it's already a usable URL in the context of the frontend.
          this.logoPreviewUrl.set(org.logoUrl);
        }
      },
      error: (err) => {
        const message = err?.error?.detail ?? err?.error?.title ?? 'Failed to load organization details.';
        this.error.set(message);
        console.error('[OrganizationForm] loadOrganization error:', err);
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
    this.successMessage.set(null);

    const formValue = this.orgForm.getRawValue();

    // Ensure businessType is sent as a number, not a string
    const sanitizedValues = {
      ...formValue,
      businessType: +formValue.businessType
    };

    const handleLogoUpload = (id: string, callback: () => void) => {
      const file = this.selectedLogoFile();
      if (file) {
        this.organizationService.uploadLogo(id, file).subscribe({
          next: () => callback(),
          error: (err) => {
            console.error('[OrganizationForm] logo upload error:', err);
            // Even if logo upload fails, the org was created/updated.
            callback();
          }
        });
      } else {
        callback();
      }
    };

    if (this.isEditMode() && this.orgId()) {
      const command: UpdateOrganizationCommand = {
        id: this.orgId()!,
        ...sanitizedValues
      };
      this.organizationService.updateOrganization(this.orgId()!, command).subscribe({
        next: () => {
          handleLogoUpload(this.orgId()!, () => {
            this.isSubmitting.set(false);
            this.successMessage.set('Organization updated successfully.');
            setTimeout(() => this.router.navigate(['/organizations']), 500);
          });
        },
        error: (err) => {
          const message = err?.error?.detail ?? err?.error?.title ?? 'Failed to update organization. Please check the inputs.';
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error('[OrganizationForm] update error:', err);
        }
      });
    } else {
      const command: CreateOrganizationCommand = { ...sanitizedValues };
      this.organizationService.createOrganization(command).subscribe({
        next: (response) => {
          handleLogoUpload(response.id, () => {
            this.isSubmitting.set(false);
            this.successMessage.set('Organization created successfully.');
            
            const isNewTenant = this.authService.currentUserSignal()?.tenantId === '00000000-0000-0000-0000-000000000000';
            if (isNewTenant) {
              this.authService.refreshSession().subscribe(() => {
                setTimeout(() => this.router.navigate(['/organizations']), 500);
              });
            } else {
              setTimeout(() => this.router.navigate(['/organizations']), 500);
            }
          });
        },
        error: (err) => {
          const message = err?.error?.detail ?? err?.error?.title ?? 'Failed to create organization. Please check the inputs.';
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error('[OrganizationForm] create error:', err);
        }
      });
    }
  }

  onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      this.selectedLogoFile.set(file);

      // Create a local preview URL
      const reader = new FileReader();
      reader.onload = (e) => {
        this.logoPreviewUrl.set(e.target?.result as string);
      };
      reader.readAsDataURL(file);
    }
  }

  onRemoveLogo(): void {
    this.selectedLogoFile.set(null);
    this.logoPreviewUrl.set(null);
    // Note: To completely clear it from the backend on edit, we would need 
    // a separate delete API, but for now we just clear the preview.
  }
}
