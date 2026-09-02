import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
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
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-branch-list',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, MatButtonModule, PageHeaderComponent, DataTableComponent, LoadingComponent, EmptyStateComponent],
  templateUrl: './branch-list.html',
  styleUrls: ['./branch-list.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BranchListComponent {
  private readonly branchService = inject(BranchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  readonly orgId = toSignal(
    this.route.paramMap.pipe(map(params => params.get('orgId') || '')),
    { initialValue: '' }
  );

  readonly isLoading = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  // Pagination & Search State
  readonly pageIndex = signal<number>(0);
  readonly pageSize = signal<number>(10);
  readonly searchTerm = signal<string>('');
  readonly statusFilter = signal<string | null>(null);

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    orgId: this.orgId(),
    search: this.searchTerm(),
    status: this.statusFilter(),
    pageNumber: this.pageIndex() + 1,
    pageSize: this.pageSize(),
    refresh: this.refreshTrigger()
  }));

  readonly branchesResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.orgId),
      tap(() => { this.isLoading.set(true); this.error.set(null); }),
      switchMap(params => this.branchService.getBranchesByOrganization(params.orgId, params.search, params.status !== null ? params.status : undefined, params.pageNumber, params.pageSize).pipe(
        catchError(err => {
          this.error.set('Failed to load branches.');
          console.error(err);
          return of({ items: [] as BranchList[], totalCount: 0 });
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [] as BranchList[], totalCount: 0 } }
  );

  readonly branches = computed(() => this.branchesResult().items);
  readonly totalCount = computed(() => this.branchesResult().totalCount);

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
        if (row.status === 'Active') { cls = 'bg-accent-50 text-accent-700 dark:bg-accent-900/30 dark:text-accent-400'; txt = 'Active'; }
        else if (row.status === 'Inactive') { cls = 'bg-yellow-50 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400'; txt = 'Inactive'; }
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

  loadBranches(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onSearch(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
    this.pageIndex.set(0);
  }

  onFilterStatus(event: any): void {
    const val = event.target.value;
    if (val === '') {
      this.statusFilter.set(null);
    } else {
      this.statusFilter.set(val);
    }
    this.pageIndex.set(0);
  }

  onPageChange(event: any): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  delete(id: string): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, { disableClose: true,
      width: '450px',
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
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, { disableClose: true,
      width: '450px',
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
