import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { BranchOption, RoleOption, TeamService } from './team.service';
import { parseApiError } from '../../../core/utils/error-parser';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { I18nService } from '../../../core/i18n/i18n.service';

export interface InviteDialogData {
  roles: RoleOption[];
  branches: BranchOption[];
}

const PHONE_PATTERN = /^\+?[0-9]{6,20}$/;

function phoneOrEmailRequired(group: AbstractControl): ValidationErrors | null {
  const phone = (group.get('phone')?.value ?? '').toString().trim();
  const email = (group.get('email')?.value ?? '').toString().trim();
  return phone || email ? null : { contactRequired: true };
}

@Component({
  selector: 'app-invite-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" class="bg-white dark:bg-gray-800 rounded-2xl flex flex-col max-h-[92vh] w-full" novalidate>
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700 flex items-center gap-3">
        <div class="w-10 h-10 rounded-xl bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
          <mat-icon>person_add</mat-icon>
        </div>
        <div>
          <h2 class="text-base font-bold text-gray-900 dark:text-white m-0">{{ 'Invite a team member' | t }}</h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 m-0">{{ 'They get a link by SMS and/or email, valid for 7 days.' | t }}</p>
        </div>
      </div>

      <div class="px-6 py-5 space-y-4 overflow-y-auto">
        <div *ngIf="error()" role="alert" class="bg-red-50 dark:bg-red-900/20 border-l-4 border-red-500 p-3 rounded-r-md text-sm text-red-700 dark:text-red-400 whitespace-pre-line">{{ error() }}</div>

        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label for="invite-phone" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">{{ 'Mobile number' | t }}</label>
            <input id="invite-phone" type="tel" formControlName="phone" placeholder="+8801XXXXXXXXX" autocomplete="off"
                   class="w-full px-3 py-2 border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-900 text-gray-900 dark:text-white">
            <p *ngIf="form.controls.phone.touched && form.controls.phone.errors?.['pattern']" class="mt-1 text-xs text-red-600">{{ 'Digits only, with an optional leading +.' | t }}</p>
          </div>
          <div>
            <label for="invite-email" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">{{ 'Email' | t }}</label>
            <input id="invite-email" type="email" formControlName="email" placeholder="name@example.com" autocomplete="off"
                   class="w-full px-3 py-2 border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-900 text-gray-900 dark:text-white">
            <p *ngIf="form.controls.email.touched && form.controls.email.errors?.['email']" class="mt-1 text-xs text-red-600">{{ 'Enter a valid email address.' | t }}</p>
          </div>
        </div>
        <p *ngIf="form.touched && form.errors?.['contactRequired']" class="-mt-2 text-xs text-red-600">{{ 'Enter a mobile number or an email address.' | t }}</p>

        <div>
          <label for="invite-role" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">{{ 'Role' | t }}</label>
          <select id="invite-role" formControlName="roleId"
                  class="w-full px-3 py-2 border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-900 text-gray-900 dark:text-white">
            <option value="" disabled>{{ 'Select a role' | t }}</option>
            <option *ngFor="let role of data.roles" [value]="role.id" [disabled]="!role.canGrant">
              {{ role.name | t }}{{ role.canGrant ? '' : (' (above your permissions)' | t) }}
            </option>
          </select>
          <p *ngIf="form.controls.roleId.touched && form.controls.roleId.invalid" class="mt-1 text-xs text-red-600">{{ 'Choose a role.' | t }}</p>
        </div>

        <div>
          <label for="invite-branch" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">{{ 'Branch (optional)' | t }}</label>
          <select id="invite-branch" formControlName="branchId"
                  class="w-full px-3 py-2 border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-900 text-gray-900 dark:text-white">
            <option value="">{{ 'All branches' | t }}</option>
            <option *ngFor="let branch of data.branches" [value]="branch.id">{{ branch.name }}</option>
          </select>
          <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ "A member assigned to a branch only sees that branch's farms." | t }}</p>
        </div>
      </div>

      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3">
        <button type="button" (click)="dialogRef.close(false)" [disabled]="saving()"
                class="px-4 py-2 rounded-lg text-sm font-semibold text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700">{{ 'Cancel' | t }}</button>
        <button type="submit" [disabled]="saving()"
                class="px-4 py-2 rounded-lg text-sm font-bold bg-primary-600 hover:bg-primary-500 text-white flex items-center gap-2 disabled:opacity-50">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'send' }}</mat-icon>
          {{ (saving() ? 'Sending…' : 'Send invitation') | t }}
        </button>
      </div>
    </form>
  `
})
export class InviteDialogComponent {
  readonly data = inject<InviteDialogData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<InviteDialogComponent, boolean>);
  private readonly fb = inject(FormBuilder);
  private readonly team = inject(TeamService);
  private readonly i18n = inject(I18nService);

  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    phone: ['', [Validators.pattern(PHONE_PATTERN)]],
    email: ['', [Validators.email, Validators.maxLength(256)]],
    roleId: [this.data.roles.find(r => r.canGrant && r.name === 'Worker')?.id ?? '', [Validators.required]],
    branchId: ['']
  }, { validators: phoneOrEmailRequired });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.team.invite({
      phone: v.phone.trim() || null,
      email: v.email.trim() || null,
      roleId: v.roleId,
      branchId: v.branchId || null
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => {
        this.saving.set(false);
        this.error.set(parseApiError(err, this.i18n.t('Could not send the invitation.')));
      }
    });
  }
}
