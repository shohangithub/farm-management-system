import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { PenService } from '../services/pen.service';
import { PenList } from '../models/pen.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-pen-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatIconModule, PageHeaderComponent],
  templateUrl: './pen-list.component.html'
})
export class PenListComponent implements OnInit {
  private readonly penService = inject(PenService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  pens = signal<PenList[]>([]);
  isLoading = signal<boolean>(true);
  branchId = signal<string>('');
  farmId = signal<string>('');
  shedId = signal<string>('');
  Math = Math;

  searchTerm = signal<string>('');
  statusFilter = signal<number | null>(null);

  filteredPens = computed(() => {
    let result = this.pens();
    
    const search = this.searchTerm().toLowerCase();
    if (search) {
      result = result.filter(p => 
        p.penName.toLowerCase().includes(search) || 
        p.penNumber.toLowerCase().includes(search)
      );
    }
    
    const status = this.statusFilter();
    if (status) {
      result = result.filter(p => p.status === status);
    }
    
    return result;
  });

  ngOnInit(): void {
    const getParams = () => {
      let currentRoute: ActivatedRoute | null = this.route;
      let branchId = '';
      let farmId = '';
      let shedId = this.route.snapshot.paramMap.get('shedId') || this.route.parent?.snapshot.paramMap.get('shedId') || '';
      
      while (currentRoute) {
        if (!branchId) branchId = currentRoute.snapshot.paramMap.get('branchId') || '';
        if (!farmId) farmId = currentRoute.snapshot.paramMap.get('farmId') || '';
        if (!shedId) shedId = currentRoute.snapshot.paramMap.get('shedId') || '';
        currentRoute = currentRoute.parent;
      }
      return { branchId, farmId, shedId };
    };
    
    const p = getParams();
    if (p.branchId && p.farmId && p.shedId) {
      this.branchId.set(p.branchId);
      this.farmId.set(p.farmId);
      this.shedId.set(p.shedId);
      this.loadPens();
    }
    
    this.route.paramMap.subscribe(() => {
      const updated = getParams();
      if (updated.shedId && updated.shedId !== this.shedId()) {
        this.branchId.set(updated.branchId);
        this.farmId.set(updated.farmId);
        this.shedId.set(updated.shedId);
        this.loadPens();
      }
    });
  }

  loadPens(): void {
    this.isLoading.set(true);
    this.penService.getPensByShed(this.shedId()).subscribe({
      next: (data) => {
        this.pens.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load pens', err);
        this.isLoading.set(false);
      }
    });
  }

  onFilterStatus(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.statusFilter.set(val ? parseInt(val, 10) : null);
  }

  onAddPen(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens', 'new']);
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
