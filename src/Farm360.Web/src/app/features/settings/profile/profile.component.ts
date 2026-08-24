import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AuthService, UserProfile } from '../../../core/services/auth.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule, PageHeaderComponent],
  templateUrl: './profile.component.html'
})
export class ProfileComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);

  profileForm = this.fb.group({
    id: [''],
    role: [{ value: '', disabled: true }],
    name: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required]
  });

  isLoading = signal(false);
  isSaving = signal(false);
  successMessage = signal<string | null>(null);
  error = signal<string | null>(null);

  ngOnInit() {
    this.isLoading.set(true);
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.profileForm.patchValue({
          id: user.id,
          role: user.role || 'Admin',
          name: (user as any).name || 'Farm Admin',
          email: (user as any).email || 'admin@farm360.ai',
          phone: (user as any).phone || '+1234567890'
        });
      }
      this.isLoading.set(false);
    });
  }

  onSubmit() {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.successMessage.set(null);
    this.error.set(null);

    const updatedData = {
      ...this.authService.currentUserSignal()!,
      ...this.profileForm.value
    } as UserProfile;

    this.authService.updateProfile(updatedData).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.successMessage.set('Profile updated successfully!');
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: (err) => {
        this.isSaving.set(false);
        this.error.set(err.message || 'Failed to update profile.');
      }
    });
  }
}
