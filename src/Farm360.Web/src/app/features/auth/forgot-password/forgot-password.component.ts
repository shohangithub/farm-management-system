import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AccountService } from '../../../core/services/account.service';
import { parseApiError } from '../../../core/utils/error-parser';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Step 1 of password reset: send a 6-digit code to the phone (same answer whether or not it has an account). */
@Component({
  selector: 'app-forgot-password',
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
          <div class="login-logo"><span class="material-icons">lock_reset</span></div>
          <h1 class="login-title">{{ 'Forgot password' | t }}</h1>
          <p class="login-subtitle">{{ "We'll text a verification code to your mobile number." | t }}</p>
        </div>

        <div *ngIf="error()" class="login-error" role="alert">
          <span class="material-icons">error_outline</span>
          <span>{{ error() | t }}</span>
        </div>

        <form class="login-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="form-group">
            <label for="phone" class="form-label">{{ 'Phone Number' | t }}</label>
            <input id="phone" type="tel" formControlName="phone" class="form-control" placeholder="+880XXXXXXXXXX" autocomplete="username">
            <span *ngIf="form.controls.phone.touched && form.controls.phone.invalid" class="field-error" role="alert">{{ 'Phone number is required.' | t }}</span>
          </div>

          <button type="submit" class="btn-login" [disabled]="saving()">
            <span *ngIf="saving()" class="login-spinner"></span>
            {{ (saving() ? 'Sending…' : 'Send code') | t }}
          </button>
        </form>

        <div class="login-footer" style="margin-top: 20px; text-align:center;">
          <a routerLink="/auth/reset-password" [queryParams]="{ phone: form.controls.phone.value || null }" style="font-size: 13px;">{{ 'I already have a code' | t }}</a>
          &middot;
          <a routerLink="/auth/login" style="font-size: 13px;">{{ 'Back to sign in' | t }}</a>
        </div>
      </div>
    </div>
  `
})
export class ForgotPasswordComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly account = inject(AccountService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly i18n = inject(I18nService);

  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    phone: ['', [Validators.required, Validators.maxLength(32)]]
  });

  ngOnInit(): void {
    const phone = this.route.snapshot.queryParamMap.get('phone');
    if (phone) this.form.patchValue({ phone });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const phone = this.form.getRawValue().phone.trim();
    this.saving.set(true);
    this.error.set(null);
    this.account.requestPasswordReset(phone).subscribe({
      next: () => this.router.navigate(['/auth/reset-password'], { queryParams: { phone, sent: 1 } }),
      error: err => {
        this.saving.set(false);
        this.error.set(parseApiError(err, this.i18n.t('Could not send the code. Please try again.')));
      }
    });
  }
}
