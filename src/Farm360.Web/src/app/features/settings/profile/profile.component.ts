import { Component, DestroyRef, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AuthService } from '../../../core/services/auth.service';
import { parseApiError } from '../../../core/utils/error-parser';
import { AccountService, PASSWORD_HINT, PASSWORD_PATTERN } from '../../../core/services/account.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { AbstractControl, ValidationErrors } from '@angular/forms';

function newPasswordsMatch(group: AbstractControl): ValidationErrors | null {
  return group.get('newPassword')?.value === group.get('confirmPassword')?.value ? null : { mismatch: true };
}

import { HasUnsavedChanges } from '../../../shared/utils/unsaved-changes.guard';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule, PageHeaderComponent],
  templateUrl: './profile.component.html'
})
export class ProfileComponent implements OnInit, HasUnsavedChanges {
  /** Set once the form is saved so the unsaved-changes guard lets navigation through. */
  private saved = false;

  hasUnsavedChanges(): boolean {
    return (!!this.profileForm?.dirty && !this.saved) || !!this.passwordForm?.dirty;
  }

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

  private accountService = inject(AccountService);
  private notify = inject(NotificationService);
  readonly passwordHint = PASSWORD_HINT;
  isChangingPassword = signal(false);
  passwordError = signal<string | null>(null);

  passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: newPasswordsMatch });

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
        this.profileForm.markAsPristine();
        this.successMessage.set('Profile updated successfully!');
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: (err) => {
        this.isSaving.set(false);
        this.error.set(parseApiError(err, 'Failed to update profile.'));
      }
    });
  }

  /** Changes the password; other devices are signed out, this one keeps a fresh session. */
  onChangePassword() {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.isChangingPassword.set(true);
    this.passwordError.set(null);
    this.accountService.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.isChangingPassword.set(false);
        this.passwordForm.reset();
        this.notify.success('Password changed. Other devices have been signed out.');
      },
      error: (err) => {
        this.isChangingPassword.set(false);
        this.passwordError.set(parseApiError(err, 'Failed to change password.'));
      }
    });
  }
}
