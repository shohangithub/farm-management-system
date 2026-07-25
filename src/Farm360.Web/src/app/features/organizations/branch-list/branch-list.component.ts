import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { BranchService } from '../services/branch.service';
import { BranchList } from '../models/branch.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';

@Component({
  selector: 'app-branch-list',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, MatButtonModule, PageHeaderComponent, DataTableComponent],
  templateUrl: './branch-list.html',
  styleUrls: ['./branch-list.scss']
})
export class BranchListComponent implements OnInit {
  private readonly branchService = inject(BranchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  orgId = signal<string>('');
  branches = signal<BranchList[]>([]);
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);

  // Pagination & Search State
  totalCount = signal<number>(0);
  pageSize = signal<number>(10);
  pageIndex = signal<number>(0);
  searchTerm = signal<string>('');
  statusFilter = signal<number | null>(null);

  displayedColumns = ['branch', 'contact', 'location', 'status', 'actions'];

  columns: TableColumn[] = [
    {
      def: 'branch',
      header: 'Branch',
      cell: (row: BranchList) => `<div><div class="font-semibold text-gray-900 dark:text-white">${row.name}</div><div class="text-[11px] text-gray-500 font-mono mt-0.5">Code: ${row.branchCode} ${row.isHeadOffice ? '<span class="ml-1 px-1.5 py-0.5 inline-flex text-[9px] leading-4 font-bold rounded-sm bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400 uppercase">HQ</span>' : ''}</div></div>`,
      isAction: false
    },
    {
      def: 'contact',
      header: 'Contact',
      cell: (row: BranchList) => `<div class="text-sm text-gray-700 dark:text-gray-300">${row.contactEmail}</div><div class="text-[11px] text-gray-500 mt-0.5">${row.contactPhone || '—'}</div>`,
      isAction: false
    },
    {
      def: 'location',
      header: 'Location',
      cell: (row: BranchList) => {
        const city = (row as any).city;
        const country = (row as any).country;
        if (city || country) {
          return `<div class="text-sm text-gray-600 dark:text-gray-400">${[city, country].filter(Boolean).join(', ')}</div>`;
        }
        return `<span class="text-[11px] text-gray-400 italic">Not specified</span>`;
      },
      isAction: false
    },
    {
      def: 'status',
      header: 'Status',
      cell: (row: BranchList) => {
        let cls = 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400';
        let txt = 'Closed';
        if (row.status === 1) { cls = 'bg-accent-50 text-accent-700 dark:bg-accent-900/30 dark:text-accent-400'; txt = 'Active'; }
        else if (row.status === 2) { cls = 'bg-yellow-50 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400'; txt = 'Inactive'; }
        return `<span class="px-2 py-0.5 inline-flex text-[11px] leading-5 font-bold rounded-md uppercase tracking-wider ${cls}">${txt}</span>`;
      },
      isAction: false
    },
    {
      def: 'actions',
      header: 'Actions',
      cell: () => '',
      isAction: true
    }
  ];

  ngOnInit(): void {
    const orgId = this.route.snapshot.paramMap.get('orgId');
    if (orgId) {
      this.orgId.set(orgId);
      this.loadBranches();
    } else {
      this.error.set('Organization ID not found in route.');
    }
  }

  loadBranches(): void {
    this.isLoading.set(true);
    const status = this.statusFilter();
    this.branchService.getBranchesByOrganization(this.orgId(), this.searchTerm(), status !== null ? status : undefined, this.pageIndex() + 1, this.pageSize()).subscribe({
      next: (data) => {
        this.branches.set(data.items);
        this.totalCount.set(data.totalCount);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load branches.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  onSearch(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
    this.pageIndex.set(0);
    this.loadBranches();
  }

  onFilterStatus(event: any): void {
    const val = event.target.value;
    if (val === '') {
      this.statusFilter.set(null);
    } else {
      this.statusFilter.set(Number(val));
    }
    this.pageIndex.set(0);
    this.loadBranches();
  }

  onPageChange(event: any): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadBranches();
  }

  delete(id: string): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Branch',
        message: 'Are you sure you want to delete this branch? This action cannot be undone.',
        confirmButtonText: 'Delete',
        cancelButtonText: 'Cancel',
        isDestructive: true
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.branchService.deleteBranch(id).subscribe({
          next: () => this.loadBranches(),
          error: (err) => console.error(err)
        });
      }
    });
  }

  onAdd(): void {
    this.router.navigate(['new'], { relativeTo: this.route });
  }

  onView(branch: BranchList): void {
    this.router.navigate(['detail', branch.id], { relativeTo: this.route });
  }

  onEdit(branch: BranchList): void {
    this.router.navigate(['edit', branch.id], { relativeTo: this.route });
  }

  onDelete(branch: BranchList): void {
    this.delete(branch.id);
  }

  onRestore(branch: BranchList): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '400px',
      data: {
        title: 'Restore Branch',
        message: 'Are you sure you want to restore this branch? It will become active again.',
        confirmButtonText: 'Restore',
        cancelButtonText: 'Cancel',
        isDestructive: false
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.branchService.activateBranch(branch.id).subscribe({
          next: () => this.loadBranches(),
          error: (err) => console.error(err)
        });
      }
    });
  }
}
