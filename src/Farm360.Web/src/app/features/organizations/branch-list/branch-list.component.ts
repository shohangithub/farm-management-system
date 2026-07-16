import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { BranchService } from '../services/branch.service';
import { BranchList } from '../models/branch.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';

@Component({
  selector: 'app-branch-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageHeaderComponent, DataTableComponent],
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

  displayedColumns = ['branch', 'contact', 'status', 'actions'];

  columns: TableColumn[] = [
    {
      def: 'branch',
      header: 'Branch',
      cell: (row: BranchList) => `<div><div class="font-semibold text-gray-900 dark:text-white">${row.name}</div><div class="text-[11px] text-gray-500">Code: ${row.branchCode} ${row.isHeadOffice ? '<span class="ml-1 px-1.5 py-0.5 inline-flex text-[9px] leading-4 font-bold rounded-sm bg-blue-100 text-blue-800 uppercase">HQ</span>' : ''}</div></div>`,
      isAction: false
    },
    {
      def: 'contact',
      header: 'Contact',
      cell: (row: BranchList) => `<div>${row.contactEmail}</div><div class="text-[11px] text-gray-500">${row.contactPhone || '-'}</div>`,
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
    this.branchService.getBranchesByOrganization(this.orgId()).subscribe({
      next: (data) => {
        this.branches.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load branches.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
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
}
