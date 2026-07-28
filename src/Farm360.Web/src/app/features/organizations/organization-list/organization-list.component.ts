import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { OrganizationService } from '../services/organization.service';
import { Organization } from '../models/organization.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap, map } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-organization-list',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, MatButtonModule, PageHeaderComponent, DataTableComponent],
  templateUrl: './organization-list.html',
  styleUrls: ['./organization-list.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OrganizationListComponent {
  private readonly organizationService = inject(OrganizationService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  readonly isLoading = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  // Pagination & Search State
  readonly pageIndex = signal<number>(0);
  readonly pageSize = signal<number>(10);
  readonly searchTerm = signal<string>('');
  readonly statusFilter = signal<number | null>(null);

  // BusinessType enum label map — must match Farm360.Domain.Organizations.Enums.BusinessType
  private readonly businessTypeLabels: Record<number, string> = {
    1: 'Farm',
    2: 'Supplier',
    3: 'Buyer',
    4: 'Veterinary Clinic',
    5: 'Cooperative'
  };

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    search: this.searchTerm(),
    status: this.statusFilter(),
    pageNumber: this.pageIndex() + 1,
    pageSize: this.pageSize(),
    refresh: this.refreshTrigger()
  }));

  readonly organizationsResult = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => { this.isLoading.set(true); this.error.set(null); }),
      switchMap(params => this.organizationService.getOrganizations(params.search, params.status !== null ? params.status : undefined, params.pageNumber, params.pageSize).pipe(
        catchError(err => {
          this.error.set('Failed to load organizations.');
          console.error(err);
          return of({ items: [] as Organization[], totalCount: 0 });
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [] as Organization[], totalCount: 0 } }
  );

  readonly organizations = computed(() => this.organizationsResult().items);
  readonly totalCount = computed(() => this.organizationsResult().totalCount);

  displayedColumns = ['name', 'contact', 'type', 'status', 'actions'];

  columns: TableColumn[] = [
    {
      def: 'name',
      header: 'Organization',
      cell: (row: Organization) => `<div class="flex items-center"><div class="flex-shrink-0 h-8 w-8 bg-primary-100 dark:bg-primary-900/30 rounded-full flex items-center justify-center text-primary-600 dark:text-primary-400 font-bold mr-3">${row.name.charAt(0).toUpperCase()}</div><div><div class="font-semibold text-gray-900 dark:text-white">${row.name}</div><div class="text-[11px] text-gray-500">${row.currencyCode} &bull; ${row.timeZoneId}</div></div></div>`,
      isAction: false
    },
    {
      def: 'contact',
      header: 'Contact',
      cell: (row: Organization) => `<div>${row.contactEmail}</div><div class="text-[11px] text-gray-500">${row.contactPhone || '-'}</div>`,
      isAction: false
    },
    {
      def: 'type',
      header: 'Type',
      cell: (row: Organization) => this.businessTypeLabels[row.businessType] ?? 'Unknown',
      isAction: false
    },
    {
      def: 'status',
      header: 'Status',
      cell: (row: Organization) => {
        const isActive = row.status === 1;
        return `<span class="px-2 py-0.5 inline-flex text-[11px] leading-5 font-bold rounded-md uppercase tracking-wider ${isActive ? 'bg-accent-50 text-accent-700 dark:bg-accent-900/30 dark:text-accent-400' : 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400'}">${isActive ? 'Active' : 'Inactive'}</span>`;
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

  loadOrganizations(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onSearch(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
    this.pageIndex.set(0); // Reset to first page
  }

  onFilterStatus(event: any): void {
    const val = event.target.value;
    if (val === '') {
      this.statusFilter.set(null);
    } else {
      this.statusFilter.set(Number(val));
    }
    this.pageIndex.set(0);
  }

  onPageChange(event: any): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  deactivate(id: string): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '400px',
      data: {
        title: 'Deactivate Organization',
        message: 'Are you sure you want to deactivate this organization?',
        confirmButtonText: 'Deactivate',
        cancelButtonText: 'Cancel',
        isDestructive: true
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.organizationService.deactivateOrganization(id).subscribe({
          next: () => this.loadOrganizations(),
          error: (err) => console.error(err)
        });
      }
    });
  }

  onAdd(): void {
    this.router.navigate(['/organizations/new']);
  }

  onView(org: Organization): void {
    this.router.navigate(['/organizations/detail', org.id]);
  }

  onEdit(org: Organization): void {
    this.router.navigate(['/organizations/edit', org.id]);
  }

  onDelete(org: Organization): void {
    this.deactivate(org.id);
  }

  onRestore(org: Organization): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '400px',
      data: {
        title: 'Restore Organization',
        message: 'Are you sure you want to restore this organization? It will become active again.',
        confirmButtonText: 'Restore',
        cancelButtonText: 'Cancel',
        isDestructive: false
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.organizationService.activateOrganization(org.id).subscribe({
          next: () => this.loadOrganizations(),
          error: (err) => console.error(err)
        });
      }
    });
  }
}
