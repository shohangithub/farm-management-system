import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { OrganizationService } from '../services/organization.service';
import { BranchService } from '../services/branch.service';
import { Organization } from '../models/organization.model';
import { BranchList } from '../models/branch.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';

@Component({
  selector: 'app-organization-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, MatTabsModule, PageHeaderComponent, DataTableComponent, EmptyStateComponent, DatePipe],
  templateUrl: './organization-detail.html',
  styleUrls: ['./organization-detail.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OrganizationDetailComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private organizationService = inject(OrganizationService);
  private branchService = inject(BranchService);

  readonly orgId = toSignal(
    this.route.paramMap.pipe(map(params => params.get('id') || '')),
    { initialValue: '' }
  );

  readonly isLoading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  // Branch table config
  branchColumns: TableColumn[] = [
    { def: 'branch', header: 'Branch', cell: (row: BranchList) => row.name },
    { def: 'contact', header: 'Contact', cell: (row: BranchList) => row.contactEmail },
    { def: 'status', header: 'Status', cell: (row: BranchList) => row.status === 'Active' ? 'Active' : 'Inactive' },
    { def: 'actions', header: 'Actions', cell: () => '' }
  ];
  branchDisplayedColumns = ['branch', 'contact', 'status', 'actions'];

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    orgId: this.orgId(),
    refresh: this.refreshTrigger()
  }));

  private dataResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.orgId),
      tap(() => { this.isLoading.set(true); this.error.set(null); }),
      switchMap(({ orgId }) => forkJoin({
        organization: this.organizationService.getOrganizationById(orgId).pipe(catchError(() => of(null))),
        branches: this.branchService.getBranchesByOrganization(orgId, '', undefined, 1, 10).pipe(
          map(res => res.items),
          catchError(() => of([]))
        )
      }).pipe(
        tap(res => {
          if (!res.organization) this.error.set('Failed to load organization details.');
        }),
        catchError(err => {
          this.error.set('Failed to load organization details.');
          return of({ organization: null, branches: [] });
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { organization: null, branches: [] } }
  );

  readonly organization = computed(() => this.dataResult().organization);
  readonly branches = computed(() => this.dataResult().branches);

  loadData(id?: string): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onEdit(): void {
    const org = this.organization();
    if (org) {
      this.router.navigate(['/organizations/edit', org.id]);
    }
  }

  getBusinessTypeLabel(type: string): string {
    switch (type) {
      case 'Farm': return 'Farm / Producer';
      case 'Supplier': return 'Supplier / Vendor';
      case 'Buyer': return 'Buyer / Distributor';
      case 'VeterinaryClinic': return 'Veterinary Clinic';
      case 'Cooperative': return 'Cooperative';
      default: return 'Unknown';
    }
  }

  onViewBranch(branch: BranchList): void {
    const org = this.organization();
    if (org) {
      this.router.navigate(['/organizations', org.id, 'branches', 'detail', branch.id]);
    }
  }

  onAddBranch(): void {
    const org = this.organization();
    if (org) {
      this.router.navigate(['/organizations', org.id, 'branches', 'new']);
    }
  }
}
