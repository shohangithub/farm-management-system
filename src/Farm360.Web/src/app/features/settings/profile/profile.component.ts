import { Component, DestroyRef, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AuthService } from '../../../core/services/auth.service';
import { parseApiError } from '../../../core/utils/error-parser';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule, PageHeaderComponent],
  templateUrl: './profile.component.html'
})
export class ProfileComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private destroyRef = inject(DestroyRef);

  profileForm = this.fb.group({
    role: [{ value: '', disabled: true }],
    name: ['', [Validators.required, Validators.maxLength(150)]],
    email: ['', [Validators.email, Validators.maxLength(256)]],
    // The phone number is the login ID; changing it needs OTP re-verification, so it's read-only here.
    phone: [{ value: '', disabled: true }]
  });

  isLoading = signal(false);
  isSaving = signal(false);
  successMessage = signal<string | null>(null);
  error = signal<string | null>(null);

  ngOnInit() {
    this.isLoading.set(true);
    this.authService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      if (user) {
        this.profileForm.patchValue({
          role: user.role ?? '',
          name: user.fullName ?? '',
          email: user.email ?? '',
          phone: user.phone ?? ''
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

    const { name, email } = this.profileForm.getRawValue();

    this.authService.updateProfile({
      fullName: (name ?? '').trim(),
      email: email?.trim() || null
    }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.successMessage.set('Profile updated successfully!');
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: (err) => {
        this.isSaving.set(false);
        this.error.set(parseApiError(err, 'Failed to update profile.'));
      }
    });
  }
}
