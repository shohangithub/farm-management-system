import { Component, OnInit, inject, signal } from '@angular/core';
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

@Component({
  selector: 'app-organization-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, MatTabsModule, PageHeaderComponent, DataTableComponent, DatePipe],
  templateUrl: './organization-detail.html',
  styleUrls: ['./organization-detail.scss']
})
export class OrganizationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private organizationService = inject(OrganizationService);
  private branchService = inject(BranchService);

  organization = signal<Organization | null>(null);
  branches = signal<BranchList[]>([]);
  isLoading = signal<boolean>(true);
  error = signal<string | null>(null);

  // Branch table config
  branchColumns: TableColumn[] = [
    { def: 'branch', header: 'Branch', cell: (row: BranchList) => row.name },
    { def: 'contact', header: 'Contact', cell: (row: BranchList) => row.contactEmail },
    { def: 'status', header: 'Status', cell: (row: BranchList) => row.status === 1 ? 'Active' : 'Inactive' },
    { def: 'actions', header: 'Actions', cell: () => '' }
  ];
  branchDisplayedColumns = ['branch', 'contact', 'status', 'actions'];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadData(id);
    } else {
      this.error.set('Organization ID not found in route.');
      this.isLoading.set(false);
    }
  }

  loadData(id: string): void {
    this.isLoading.set(true);
    
    // Load Org Details
    this.organizationService.getOrganizationById(id).subscribe({
      next: (org) => {
        this.organization.set(org);
        
        // Load Branches after Org is loaded
        this.branchService.getBranchesByOrganization(id, '', undefined, 1, 10).subscribe({
          next: (branchData) => {
            this.branches.set(branchData.items);
            this.isLoading.set(false);
          },
          error: (err) => {
            console.error('Failed to load branches', err);
            // Don't fail the whole page if branches fail
            this.isLoading.set(false);
          }
        });
      },
      error: (err) => {
        this.error.set('Failed to load organization details.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  onEdit(): void {
    const org = this.organization();
    if (org) {
      this.router.navigate(['/organizations/edit', org.id]);
    }
  }

  getBusinessTypeLabel(type: number): string {
    switch (type) {
      case 1: return 'Farm / Producer';
      case 2: return 'Supplier / Vendor';
      case 3: return 'Buyer / Distributor';
      case 4: return 'Veterinary Clinic';
      case 5: return 'Cooperative';
      default: return 'Unknown';
    }
  }

  onViewBranch(branch: BranchList): void {
    const org = this.organization();
    if (org) {
      this.router.navigate(['/organizations', org.id, 'branches', 'detail', branch.id]);
    }
  }
}
