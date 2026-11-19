import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AccountService } from '../../../core/services/account.service';
import { parseApiError } from '../../../core/utils/error-parser';

/** Step 1 of password reset: send a 6-digit code to the phone (same answer whether or not it has an account). */
@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  styleUrls: ['../login/login.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="login-page">
      <div class="login-card">
        <div class="login-brand">
          <div class="login-logo"><span class="material-icons">lock_reset</span></div>
          <h1 class="login-title">Forgot password</h1>
          <p class="login-subtitle">We'll text a verification code to your mobile number.</p>
        </div>

        <div *ngIf="error()" class="login-error" role="alert">
          <span class="material-icons">error_outline</span>
          <span>{{ error() }}</span>
        </div>

        <form class="login-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="form-group">
            <label for="phone" class="form-label">Phone Number</label>
            <input id="phone" type="tel" formControlName="phone" class="form-control" placeholder="+880XXXXXXXXXX" autocomplete="username">
            <span *ngIf="form.controls.phone.touched && form.controls.phone.invalid" class="field-error" role="alert">Phone number is required.</span>
          </div>

          <button type="submit" class="btn-login" [disabled]="saving()">
            <span *ngIf="saving()" class="login-spinner"></span>
            {{ saving() ? 'Sending…' : 'Send code' }}
          </button>
        </form>

        <div class="login-footer" style="margin-top: 20px; text-align:center;">
          <a routerLink="/auth/reset-password" [queryParams]="{ phone: form.controls.phone.value || null }" style="font-size: 13px;">I already have a code</a>
          &middot;
          <a routerLink="/auth/login" style="font-size: 13px;">Back to sign in</a>
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
        this.error.set(parseApiError(err, 'Could not send the code. Please try again.'));
      }
    });
  }
}
