import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AccountService, InvitationPreview, PASSWORD_HINT, PASSWORD_PATTERN } from '../../../core/services/account.service';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { parseApiError } from '../../../core/utils/error-parser';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  return group.get('password')?.value === group.get('confirmPassword')?.value ? null : { mismatch: true };
}

/**
 * Public page opened from the invitation link (/auth/accept-invitation?token=…).
 * Existing account → confirm its password to join. New person → name + password (+ phone for email-only invites).
 */
@Component({
  selector: 'app-accept-invitation',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './accept-invitation.component.html',
  styleUrls: ['../login/login.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AcceptInvitationComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly account = inject(AccountService);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotificationService);

  readonly passwordHint = PASSWORD_HINT;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly preview = signal<InvitationPreview | null>(null);
  readonly invalidMessage = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  private token = '';

  readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(150)]],
    phone: ['', [Validators.pattern(/^\+?[0-9]{6,20}$/)]],
    password: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: passwordsMatch });

  /** Existing account: its current password, so a forwarded link can't add someone else's account. */
  readonly joinForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]]
  });

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.token) {
      this.loading.set(false);
      this.invalidMessage.set('This invitation link is incomplete. Open the link from your SMS or email again.');
      return;
    }

    this.account.previewInvitation(this.token).subscribe({
      next: p => {
        this.preview.set(p);
        if (p.needsPhone) this.form.controls.phone.addValidators(Validators.required);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.invalidMessage.set(parseApiError(err, 'This invitation link is invalid or has expired.'));
      }
    });
  }

  /** Existing account: joins after confirming the account's password. */
  join(): void {
    if (this.joinForm.invalid) {
      this.joinForm.markAllAsTouched();
      return;
    }
    this.submit({ token: this.token, password: this.joinForm.getRawValue().currentPassword });
  }

  createAccount(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.submit({
      token: this.token,
      fullName: v.fullName.trim(),
      password: v.password,
      phone: this.preview()?.needsPhone ? v.phone.trim() : null
    });
  }

  private submit(request: { token: string; fullName?: string; password?: string; phone?: string | null }): void {
    this.saving.set(true);
    this.error.set(null);
    this.account.acceptInvitation(request).subscribe({
      next: result => {
        this.notify.success(result.alreadyMember
          ? `You're already a member of ${result.tenantName}.`
          : `You've joined ${result.tenantName}. Sign in to continue.`);
        // A signed-in session belongs to the previous membership; signing in again picks up the new one.
        if (this.auth.isAuthenticated) this.auth.clearSession();
        this.router.navigate(['/auth/login']);
      },
      error: err => {
        this.saving.set(false);
        this.error.set(parseApiError(err, 'Could not accept the invitation.'));
      }
    });
  }
}
