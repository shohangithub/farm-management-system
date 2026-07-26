import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';
import { BranchService } from '../services/branch.service';
import { Branch } from '../models/branch.model';
import { FarmService } from '../../farms/services/farm.service';
import { FarmList } from '../../farms/models/farm.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-branch-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    MatTabsModule,
    MatDividerModule,
    PageHeaderComponent,
    DatePipe
  ],
  templateUrl: './branch-detail.html',
  styleUrls: ['./branch-detail.scss']
})
export class BranchDetailComponent implements OnInit {
  private readonly branchService = inject(BranchService);
  private readonly farmService = inject(FarmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  orgId = signal<string>('');
  branch = signal<Branch | null>(null);
  farms = signal<FarmList[]>([]);
  isLoading = signal<boolean>(false);
  isLoadingFarms = signal<boolean>(false);
  error = signal<string | null>(null);

  statusLabel = computed(() => {
    const s = this.branch()?.status;
    if (s === 1) return 'Active';
    if (s === 2) return 'Inactive';
    return 'Closed';
  });

  statusClass = computed(() => {
    const s = this.branch()?.status;
    if (s === 1) return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
    if (s === 2) return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
    return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
  });

  ngOnInit(): void {
    const orgId = this.route.snapshot.paramMap.get('orgId');
    const branchId = this.route.snapshot.paramMap.get('branchId');

    if (orgId && branchId) {
      this.orgId.set(orgId);
      this.loadBranch(branchId);
    } else {
      this.error.set('Organization ID or Branch ID not found in route.');
    }
  }

  loadBranch(id: string): void {
    this.isLoading.set(true);
    this.branchService.getBranchById(id).subscribe({
      next: (data) => {
        this.branch.set(data);
        this.isLoading.set(false);
        this.loadFarms(id);
      },
      error: (err) => {
        this.error.set('Failed to load branch details.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  loadFarms(branchId: string): void {
    this.isLoadingFarms.set(true);
    this.farmService.getFarmsByBranch(branchId).subscribe({
      next: (data) => {
        this.farms.set(data);
        this.isLoadingFarms.set(false);
      },
      error: () => {
        this.isLoadingFarms.set(false);
      }
    });
  }

  onEdit(): void {
    if (this.branch()) {
      this.router.navigate(['/organizations', this.orgId(), 'branches', 'edit', this.branch()?.id]);
    }
  }

  onBack(): void {
    this.router.navigate(['/organizations', this.orgId(), 'branches']);
  }

  onManageFarms(): void {
    if (this.branch()) {
      this.router.navigate(['/organizations', 'branches', this.branch()!.id, 'farms']);
    }
  }

  formatAddress(branch: Branch | null): string {
    if (!branch?.address) return '';
    const parts = [
      branch.address.street,
      branch.address.city,
      branch.address.state,
      branch.address.zipCode,
      branch.address.country
    ].filter(Boolean);
    return parts.join(', ');
  }

  hasAddress(branch: Branch | null): boolean {
    return !!(branch?.address?.street || branch?.address?.city || branch?.address?.country);
  }
}
