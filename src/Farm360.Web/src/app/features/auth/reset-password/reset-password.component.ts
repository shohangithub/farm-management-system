import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { map, of, switchMap, tap } from 'rxjs';
import { AccountService, PASSWORD_HINT, PASSWORD_PATTERN } from '../../../core/services/account.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { parseApiError } from '../../../core/utils/error-parser';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  return group.get('newPassword')?.value === group.get('confirmPassword')?.value ? null : { mismatch: true };
}

/** Step 2 of password reset: verify the SMS code, then set a new password (all sessions are signed out). */
@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, TranslatePipe],
  styleUrls: ['../login/login.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="login-page">
      <!-- Language switch: shows the language you can switch to -->
      <button type="button" (click)="i18n.toggle()"
        [attr.aria-label]="(i18n.isBangla ? 'Switch to English' : 'Switch to Bangla') | t"
        class="fixed top-4 right-4 z-50 h-9 px-4 rounded-full text-sm font-semibold border border-gray-200 dark:border-gray-700 bg-white/90 dark:bg-gray-800/90 text-gray-700 dark:text-gray-200 shadow-sm hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">
        {{ i18n.isBangla ? 'English' : 'বাংলা' }}
      </button>
      <div class="login-card">
        <div class="login-brand">
          <div class="login-logo"><span class="material-icons">password</span></div>
          <h1 class="login-title">{{ 'Reset password' | t }}</h1>
          <p class="login-subtitle">{{ 'Enter the code we texted you and choose a new password.' | t }}</p>
        </div>

        <div *ngIf="sent" class="login-error" role="status" style="background: rgba(16,185,129,0.12); border-color: rgba(16,185,129,0.35); color: #10b981;">
          <span class="material-icons">sms</span>
          <span>{{ 'If an account exists for this number, a 6-digit code has been sent. It expires in 5 minutes.' | t }}</span>
        </div>

        <div *ngIf="error()" class="login-error" role="alert">
          <span class="material-icons">error_outline</span>
          <span style="white-space: pre-line;">{{ error() | t }}</span>
        </div>

        <form class="login-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="form-group">
            <label for="phone" class="form-label">{{ 'Phone Number' | t }}</label>
            <input id="phone" type="tel" formControlName="phone" class="form-control" autocomplete="username">
            <span *ngIf="form.controls.phone.touched && form.controls.phone.invalid" class="field-error" role="alert">{{ 'Phone number is required.' | t }}</span>
          </div>

          <div class="form-group">
            <label for="code" class="form-label">{{ 'Verification code' | t }}</label>
            <input id="code" type="text" inputmode="numeric" maxlength="6" formControlName="code" class="form-control" autocomplete="one-time-code">
            <span *ngIf="form.controls.code.touched && form.controls.code.invalid" class="field-error" role="alert">{{ 'Enter the 6-digit code.' | t }}</span>
          </div>

          <div class="form-group">
            <label for="newPassword" class="form-label">{{ 'New password' | t }}</label>
            <input id="newPassword" type="password" formControlName="newPassword" class="form-control" autocomplete="new-password">
            <span *ngIf="form.controls.newPassword.touched && form.controls.newPassword.invalid" class="field-error" role="alert">{{ passwordHint | t }}</span>
          </div>

          <div class="form-group">
            <label for="confirmPassword" class="form-label">{{ 'Confirm new password' | t }}</label>
            <input id="confirmPassword" type="password" formControlName="confirmPassword" class="form-control" autocomplete="new-password">
            <span *ngIf="form.controls.confirmPassword.touched && form.errors?.['mismatch']" class="field-error" role="alert">{{ "Passwords don't match." | t }}</span>
          </div>

          <button type="submit" class="btn-login" [disabled]="saving()">
            <span *ngIf="saving()" class="login-spinner"></span>
            {{ (saving() ? 'Saving…' : 'Set new password') | t }}
          </button>
        </form>

        <div class="login-footer" style="margin-top: 20px; text-align:center;">
          <a routerLink="/auth/forgot-password" [queryParams]="{ phone: form.controls.phone.value || null }" style="font-size: 13px;">{{ 'Send a new code' | t }}</a>
          &middot;
          <a routerLink="/auth/login" style="font-size: 13px;">{{ 'Back to sign in' | t }}</a>
        </div>
      </div>
    </div>
  `
})
export class ResetPasswordComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly account = inject(AccountService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly notify = inject(NotificationService);
  readonly i18n = inject(I18nService);

  readonly passwordHint = PASSWORD_HINT;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  sent = false;
  private resetToken: string | null = null;

  readonly form = this.fb.nonNullable.group({
    phone: ['', [Validators.required, Validators.maxLength(32)]],
    code: ['', [Validators.required, Validators.pattern(/^[0-9]{6}$/)]],
    newPassword: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: passwordsMatch });

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const phone = params.get('phone');
    if (phone) this.form.patchValue({ phone });
    this.sent = params.get('sent') === '1';
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    // The code is single-use: once verified, keep the reset token so a retry (e.g. after a
    // rejected password) does not need a new code.
    const token$ = this.resetToken
      ? of(this.resetToken)
      : this.account.verifyResetCode(v.phone.trim(), v.code.trim()).pipe(
          map(t => t.resetToken),
          tap(t => (this.resetToken = t))
        );
    token$.pipe(
      switchMap(token => this.account.resetPassword(token, v.newPassword))
    ).subscribe({
      next: () => {
        this.notify.success(this.i18n.t('Password changed. Sign in with your new password.'));
        this.router.navigate(['/auth/login'], { queryParams: { phone: v.phone.trim() } });
      },
      error: err => {
        this.saving.set(false);
        this.error.set(parseApiError(err, this.i18n.t('Could not reset the password.')));
      }
    });
  }
}
