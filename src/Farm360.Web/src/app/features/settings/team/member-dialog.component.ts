import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Observable, concat, toArray } from 'rxjs';
import { BranchOption, RoleOption, TeamMember, TeamService } from './team.service';
import { parseApiError } from '../../../core/utils/error-parser';

export interface MemberDialogData {
  member: TeamMember;
  roles: RoleOption[];
  branches: BranchOption[];
  canAssignRole: boolean;
  canEditBranch: boolean;
}

/** Changes a member's role and/or branch assignment. */
@Component({
  selector: 'app-member-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" class="bg-white dark:bg-gray-800 rounded-2xl flex flex-col max-h-[92vh] w-full" novalidate>
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700">
        <h2 class="text-base font-bold text-gray-900 dark:text-white m-0">Edit access</h2>
        <p class="text-xs text-gray-500 dark:text-gray-400 m-0">{{ data.member.fullName || data.member.phone || data.member.email }}</p>
      </div>

      <div class="px-6 py-5 space-y-4">
        <div *ngIf="error()" role="alert" class="bg-red-50 dark:bg-red-900/20 border-l-4 border-red-500 p-3 rounded-r-md text-sm text-red-700 dark:text-red-400 whitespace-pre-line">{{ error() }}</div>

        <div>
          <label for="member-role" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Role</label>
          <select id="member-role" formControlName="roleId"
                  class="w-full px-3 py-2 border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-900 text-gray-900 dark:text-white disabled:opacity-60">
            <option *ngFor="let role of data.roles" [value]="role.id" [disabled]="!role.canGrant && role.id !== data.member.roleId">
              {{ role.name }}{{ role.canGrant ? '' : ' (above your permissions)' }}
            </option>
          </select>
          <p *ngIf="!data.canAssignRole" class="mt-1 text-xs text-gray-500">You don't have permission to change roles.</p>
          <p *ngIf="form.controls.roleId.touched && form.controls.roleId.invalid" class="mt-1 text-xs text-red-600">Choose a role.</p>
        </div>

        <div>
          <label for="member-branch" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Branch</label>
          <select id="member-branch" formControlName="branchId"
                  class="w-full px-3 py-2 border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-900 text-gray-900 dark:text-white disabled:opacity-60">
            <option value="">All branches</option>
            <option *ngFor="let branch of data.branches" [value]="branch.id">{{ branch.name }}</option>
          </select>
          <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Changes apply to the member's next request.</p>
        </div>
      </div>

      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3">
        <button type="button" (click)="dialogRef.close(false)" [disabled]="saving()"
                class="px-4 py-2 rounded-lg text-sm font-semibold text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700">Cancel</button>
        <button type="submit" [disabled]="saving() || form.pristine"
                class="px-4 py-2 rounded-lg text-sm font-bold bg-primary-600 hover:bg-primary-500 text-white flex items-center gap-2 disabled:opacity-50">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</mat-icon>
          {{ saving() ? 'Saving…' : 'Save' }}
        </button>
      </div>
    </form>
  `
})
export class MemberDialogComponent {
  readonly data = inject<MemberDialogData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<MemberDialogComponent, boolean>);
  private readonly fb = inject(FormBuilder);
  private readonly team = inject(TeamService);

  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    roleId: [{ value: this.data.member.roleId, disabled: !this.data.canAssignRole }, [Validators.required]],
    branchId: [{ value: this.data.member.branchId ?? '', disabled: !this.data.canEditBranch }]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const member = this.data.member;
    const calls: Observable<void>[] = [];
    if (this.data.canAssignRole && v.roleId !== member.roleId) calls.push(this.team.changeRole(member.id, v.roleId));
    const branchId = v.branchId || null;
    if (this.data.canEditBranch && branchId !== (member.branchId ?? null)) calls.push(this.team.changeBranch(member.id, branchId));
    if (calls.length === 0) {
      this.dialogRef.close(false);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    concat(...calls).pipe(toArray()).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => {
        this.saving.set(false);
        this.error.set(parseApiError(err, 'Could not update the member.'));
      }
    });
  }
}
