import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { ShedService } from '../services/shed.service';
import { ShedList } from '../models/shed.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-shed-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatIconModule, PageHeaderComponent],
  templateUrl: './shed-list.component.html'
})
export class ShedListComponent implements OnInit {
  private readonly shedService = inject(ShedService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  sheds = signal<ShedList[]>([]);
  isLoading = signal<boolean>(true);
  farmId = signal<string>('');
  branchId = signal<string>('');
  Math = Math;

  searchTerm = signal<string>('');
  statusFilter = signal<number | null>(null);

  filteredSheds = computed(() => {
    let result = this.sheds();
    
    const search = this.searchTerm().toLowerCase();
    if (search) {
      result = result.filter(s => 
        s.shedName.toLowerCase().includes(search) || 
        s.shedNumber.toLowerCase().includes(search)
      );
    }
    
    const status = this.statusFilter();
    if (status) {
      result = result.filter(s => s.status === status);
    }
    
    return result;
  });

  ngOnInit(): void {
    const getParams = () => ({
      farmId: this.route.snapshot.paramMap.get('farmId') || this.route.parent?.snapshot.paramMap.get('farmId') || '',
      branchId: this.route.snapshot.paramMap.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId') || ''
    });
    
    const p = getParams();
    if (p.farmId && p.branchId) {
      this.farmId.set(p.farmId);
      this.branchId.set(p.branchId);
      this.loadSheds();
    }
    
    this.route.paramMap.subscribe(() => {
      const updated = getParams();
      if (updated.farmId && updated.farmId !== this.farmId()) {
        this.farmId.set(updated.farmId);
        this.branchId.set(updated.branchId);
        this.loadSheds();
      }
    });
  }

  loadSheds(): void {
    this.isLoading.set(true);
    this.shedService.getShedsByFarm(this.farmId()).subscribe({
      next: (data) => {
        this.sheds.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load sheds', err);
        this.isLoading.set(false);
      }
    });
  }

  onFilterStatus(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.statusFilter.set(val ? parseInt(val, 10) : null);
  }

  onAddShed(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', 'new']);
  }

  getStatusName(status: number): string {
    switch (status) {
      case 1: return 'Active';
      case 2: return 'Inactive';
      case 3: return 'Maintenance';
      default: return 'Unknown';
    }
  }

  getStatusClass(status: number): string {
    switch (status) {
      case 1: return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
      case 2: return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
      case 3: return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
      default: return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
    }
  }
}
