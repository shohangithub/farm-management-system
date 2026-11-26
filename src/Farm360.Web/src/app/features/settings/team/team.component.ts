import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, filter, forkJoin, switchMap } from 'rxjs';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { ConfirmService } from '../../../shared/services/confirm.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/auth/permissions';
import { parseApiError } from '../../../core/utils/error-parser';
import { BranchOption, RoleOption, TeamInvitation, TeamMember, TeamService } from './team.service';
import { InviteDialogComponent, InviteDialogData } from './invite-dialog.component';
import { MemberDialogComponent, MemberDialogData } from './member-dialog.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { I18nService } from '../../../core/i18n/i18n.service';

/** Settings → Team: members of the organisation, their roles/branches, and pending invitations. */
@Component({
  selector: 'app-team',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatTooltipModule, PageHeaderComponent, ErrorStateComponent, HasPermissionDirective, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      [title]="'Team' | t"
      [description]="'Invite people, set their role and branch, and manage access to your organisation.' | t"
      [primaryActionLabel]="canInvite() ? ('Invite member' | t) : undefined"
      primaryActionIcon="person_add"
      (primaryAction)="openInvite()">
    </app-page-header>

    <div class="space-y-6">
      <div *ngIf="loading()" class="text-sm text-gray-500 dark:text-gray-400 flex items-center gap-2">
        <mat-icon class="animate-spin">autorenew</mat-icon> {{ 'Loading team…' | t }}
      </div>

      <app-error-state *ngIf="loadError()" [title]="'Could not load your team' | t" [message]="loadError()!" (retry)="load()"></app-error-state>

      <ng-container *ngIf="!loading() && !loadError()">
        <!-- Members -->
        <section class="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-700 overflow-hidden">
          <div class="px-4 sm:px-6 py-4 border-b border-gray-100 dark:border-gray-700 flex items-center justify-between">
            <h3 class="text-base font-bold text-gray-900 dark:text-white m-0">{{ 'Members ({n})' | t: { n: members().length } }}</h3>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-gray-50 dark:bg-gray-900/40 text-left text-xs uppercase tracking-wide text-gray-500 dark:text-gray-400">
                <tr>
                  <th class="px-4 py-3">{{ 'Name' | t }}</th>
                  <th class="px-4 py-3">{{ 'Contact' | t }}</th>
                  <th class="px-4 py-3">{{ 'Role' | t }}</th>
                  <th class="px-4 py-3">{{ 'Branch' | t }}</th>
                  <th class="px-4 py-3">{{ 'Status' | t }}</th>
                  <th class="px-4 py-3">{{ 'Last sign-in' | t }}</th>
                  <th class="px-4 py-3 text-right">{{ 'Actions' | t }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                <tr *ngFor="let m of members(); trackBy: trackById" class="text-gray-800 dark:text-gray-200">
                  <td class="px-4 py-3 font-medium">
                    {{ m.fullName || '—' }}
                    <span *ngIf="m.isCurrentUser" class="ml-1 text-xs text-gray-500">{{ '(you)' | t }}</span>
                  </td>
                  <td class="px-4 py-3">
                    <div>{{ m.phone || '—' }}</div>
                    <div *ngIf="m.email" class="text-xs text-gray-500">{{ m.email }}</div>
                  </td>
                  <td class="px-4 py-3">
                    <span class="inline-flex items-center gap-1">
                      <mat-icon *ngIf="m.isOwner" class="!text-[16px] !w-[16px] !h-[16px] text-amber-500" [matTooltip]="'Owner' | t">star</mat-icon>
                      {{ m.roleName | t }}
                    </span>
                  </td>
                  <td class="px-4 py-3">{{ m.branchName || ('All branches' | t) }}</td>
                  <td class="px-4 py-3">
                    <span class="px-2 py-0.5 rounded-full text-xs font-semibold"
                          [class.bg-emerald-50]="m.status === 'Active'" [class.text-emerald-700]="m.status === 'Active'"
                          [class.bg-gray-100]="m.status !== 'Active'" [class.text-gray-600]="m.status !== 'Active'">
                      {{ m.status | t }}
                    </span>
                  </td>
                  <td class="px-4 py-3 whitespace-nowrap">{{ m.lastLoginAt ? (m.lastLoginAt | date: 'medium') : ('Never' | t) }}</td>
                  <td class="px-4 py-3 text-right whitespace-nowrap">
                    <ng-container *ngIf="!m.isCurrentUser && !busy().has(m.id)">
                      <button *hasPermission="[perms.Edit, perms.AssignRole]" type="button" (click)="openEdit(m)"
                              class="p-1.5 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700" [matTooltip]="'Change role / branch' | t" [attr.aria-label]="'Change role or branch' | t">
                        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">manage_accounts</mat-icon>
                      </button>
                      <ng-container *hasPermission="perms.Edit">
                        <button *ngIf="m.status === 'Active'" type="button" (click)="deactivate(m)"
                                class="p-1.5 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700" [matTooltip]="'Deactivate' | t" [attr.aria-label]="'Deactivate' | t">
                          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">block</mat-icon>
                        </button>
                        <button *ngIf="m.status === 'Deactivated'" type="button" (click)="reactivate(m)"
                                class="p-1.5 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700" [matTooltip]="'Reactivate' | t" [attr.aria-label]="'Reactivate' | t">
                          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">restart_alt</mat-icon>
                        </button>
                      </ng-container>
                      <button *hasPermission="perms.Remove" type="button" (click)="remove(m)"
                              class="p-1.5 rounded-lg hover:bg-red-50 dark:hover:bg-red-900/20 text-red-600" [matTooltip]="'Remove from organisation' | t" [attr.aria-label]="'Remove' | t">
                        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">person_remove</mat-icon>
                      </button>
                    </ng-container>
                    <mat-icon *ngIf="busy().has(m.id)" class="animate-spin !text-[18px] !w-[18px] !h-[18px]">autorenew</mat-icon>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Pending invitations -->
        <section class="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-700 overflow-hidden">
          <div class="px-4 sm:px-6 py-4 border-b border-gray-100 dark:border-gray-700">
            <h3 class="text-base font-bold text-gray-900 dark:text-white m-0">{{ 'Pending invitations ({n})' | t: { n: invitations().length } }}</h3>
          </div>
          <p *ngIf="invitations().length === 0" class="px-6 py-6 text-sm text-gray-500 dark:text-gray-400 m-0">{{ 'No pending invitations.' | t }}</p>
          <div *ngIf="invitations().length > 0" class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-gray-50 dark:bg-gray-900/40 text-left text-xs uppercase tracking-wide text-gray-500 dark:text-gray-400">
                <tr>
                  <th class="px-4 py-3">{{ 'Invitee' | t }}</th>
                  <th class="px-4 py-3">{{ 'Role' | t }}</th>
                  <th class="px-4 py-3">{{ 'Branch' | t }}</th>
                  <th class="px-4 py-3">{{ 'Status' | t }}</th>
                  <th class="px-4 py-3">{{ 'Expires' | t }}</th>
                  <th class="px-4 py-3 text-right">{{ 'Actions' | t }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                <tr *ngFor="let i of invitations(); trackBy: trackById" class="text-gray-800 dark:text-gray-200">
                  <td class="px-4 py-3">
                    <div>{{ i.phone || i.email }}</div>
                    <div *ngIf="i.phone && i.email" class="text-xs text-gray-500">{{ i.email }}</div>
                    <div *ngIf="i.invitedByName" class="text-xs text-gray-500">{{ 'Invited by {name}' | t: { name: i.invitedByName } }}</div>
                  </td>
                  <td class="px-4 py-3">{{ i.roleName | t }}</td>
                  <td class="px-4 py-3">{{ i.branchName || ('All branches' | t) }}</td>
                  <td class="px-4 py-3">
                    <span class="px-2 py-0.5 rounded-full text-xs font-semibold"
                          [class.bg-amber-50]="i.status === 'Pending'" [class.text-amber-700]="i.status === 'Pending'"
                          [class.bg-gray-100]="i.status !== 'Pending'" [class.text-gray-600]="i.status !== 'Pending'">
                      {{ i.status | t }}
                    </span>
                  </td>
                  <td class="px-4 py-3 whitespace-nowrap">{{ i.expiresAtUtc | date: 'medium' }}</td>
                  <td class="px-4 py-3 text-right whitespace-nowrap">
                    <ng-container *hasPermission="perms.Invite">
                      <ng-container *ngIf="!busy().has(i.id)">
                        <button type="button" (click)="resend(i)" class="p-1.5 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700" [matTooltip]="'Resend (new link)' | t" [attr.aria-label]="'Resend' | t">
                          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">forward_to_inbox</mat-icon>
                        </button>
                        <button type="button" (click)="revoke(i)" class="p-1.5 rounded-lg hover:bg-red-50 dark:hover:bg-red-900/20 text-red-600" [matTooltip]="'Revoke' | t" [attr.aria-label]="'Revoke' | t">
                          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">cancel</mat-icon>
                        </button>
                      </ng-container>
                      <mat-icon *ngIf="busy().has(i.id)" class="animate-spin !text-[18px] !w-[18px] !h-[18px]">autorenew</mat-icon>
                    </ng-container>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </ng-container>
    </div>
  `
})
export class TeamComponent implements OnInit {
  private readonly team = inject(TeamService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18nService);

  readonly perms = Permissions.Users;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly members = signal<TeamMember[]>([]);
  readonly invitations = signal<TeamInvitation[]>([]);
  readonly roles = signal<RoleOption[]>([]);
  readonly branches = signal<BranchOption[]>([]);
  /** Rows with a request in flight (actions disabled meanwhile). */
  readonly busy = signal<ReadonlySet<string>>(new Set());

  readonly canInvite = computed(() => {
    this.auth.currentUserSignal();
    return this.auth.hasPermission(Permissions.Users.Invite);
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    forkJoin({
      members: this.team.getMembers(),
      invitations: this.team.getInvitations(),
      roles: this.team.getRoles(),
      branches: this.team.getBranches()
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.members.set(r.members);
        this.invitations.set(r.invitations);
        this.roles.set(r.roles);
        this.branches.set(r.branches);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.loadError.set(parseApiError(err, this.i18n.t('Something went wrong while loading your team.')));
      }
    });
  }

  trackById(_: number, row: { id: string }): string {
    return row.id;
  }

  openInvite(): void {
    if (!this.canInvite()) return;
    const data: InviteDialogData = { roles: this.roles(), branches: this.branches() };
    this.dialog.open(InviteDialogComponent, { width: '560px', maxWidth: '95vw', data, autoFocus: 'first-tabbable' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.notify.success(this.i18n.t('Invitation sent.'));
        this.refreshInvitations();
      });
  }

  openEdit(member: TeamMember): void {
    const data: MemberDialogData = {
      member,
      roles: this.roles(),
      branches: this.branches(),
      canAssignRole: this.auth.hasPermission(Permissions.Users.AssignRole),
      canEditBranch: this.auth.hasPermission(Permissions.Users.Edit)
    };
    this.dialog.open(MemberDialogComponent, { width: '480px', maxWidth: '95vw', data })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.notify.success(this.i18n.t('Member updated.'));
        this.refreshMembers();
      });
  }

  deactivate(m: TeamMember): void {
    this.runConfirmed(m.id, {
      title: this.i18n.t('Deactivate member?'),
      message: this.i18n.t("{name} will be signed out on all devices and won't be able to access this organisation until reactivated.", { name: this.label(m) }),
      confirmText: this.i18n.t('Deactivate'),
      destructive: true
    }, () => this.team.deactivate(m.id), this.i18n.t('Member deactivated.'), () => this.refreshMembers());
  }

  reactivate(m: TeamMember): void {
    this.runConfirmed(m.id, {
      title: this.i18n.t('Reactivate member?'),
      message: this.i18n.t('{name} will regain access with their current role. This uses one user seat on your plan.', { name: this.label(m) }),
      confirmText: this.i18n.t('Reactivate')
    }, () => this.team.reactivate(m.id), this.i18n.t('Member reactivated.'), () => this.refreshMembers());
  }

  remove(m: TeamMember): void {
    this.runConfirmed(m.id, {
      title: this.i18n.t('Remove from organisation?'),
      message: this.i18n.t('{name} will lose access to this organisation and be signed out. You can invite them again later.', { name: this.label(m) }),
      confirmText: this.i18n.t('Remove'),
      destructive: true
    }, () => this.team.remove(m.id), this.i18n.t('Member removed.'), () => this.refreshMembers());
  }

  resend(i: TeamInvitation): void {
    this.runConfirmed(i.id, {
      title: this.i18n.t('Resend invitation?'),
      message: this.i18n.t('A new link valid for 7 days will be sent to {contact}. The previous link stops working.', { contact: i.phone || i.email }),
      confirmText: this.i18n.t('Resend')
    }, () => this.team.resendInvitation(i.id), this.i18n.t('Invitation resent.'), () => this.refreshInvitations());
  }

  revoke(i: TeamInvitation): void {
    this.runConfirmed(i.id, {
      title: this.i18n.t('Revoke invitation?'),
      message: this.i18n.t('The invitation link sent to {contact} will stop working.', { contact: i.phone || i.email }),
      confirmText: this.i18n.t('Revoke'),
      destructive: true
    }, () => this.team.revokeInvitation(i.id), this.i18n.t('Invitation revoked.'), () => this.refreshInvitations());
  }

  private runConfirmed(
    id: string,
    options: { title: string; message: string; confirmText: string; destructive?: boolean },
    action: () => Observable<unknown>,
    successMessage: string,
    refresh: () => void
  ): void {
    this.confirm.confirm(options).pipe(
      filter(Boolean),
      switchMap(() => {
        this.setBusy(id, true);
        return action();
      })
    ).subscribe({
      next: () => {
        this.setBusy(id, false);
        this.notify.success(successMessage);
        refresh();
      },
      error: err => {
        this.setBusy(id, false);
        this.notify.error(parseApiError(err, this.i18n.t('The action failed.')));
      }
    });
  }

  private refreshMembers(): void {
    this.team.getMembers().subscribe({
      next: list => this.members.set(list),
      error: err => this.notify.error(parseApiError(err, this.i18n.t('Could not refresh members.')))
    });
  }

  private refreshInvitations(): void {
    this.team.getInvitations().subscribe({
      next: list => this.invitations.set(list),
      error: err => this.notify.error(parseApiError(err, this.i18n.t('Could not refresh invitations.')))
    });
  }

  private setBusy(id: string, on: boolean): void {
    const next = new Set(this.busy());
    if (on) next.add(id); else next.delete(id);
    this.busy.set(next);
  }

  private label(m: TeamMember): string {
    return m.fullName || m.phone || m.email || this.i18n.t('This member');
  }
}
