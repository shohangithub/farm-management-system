import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';
import { FarmService } from '../services/farm.service';
import { Farm } from '../models/farm.model';
import { ShedService } from '../services/shed.service';
import { ShedList } from '../models/shed.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-farm-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    MatTabsModule,
    MatDividerModule,
    PageHeaderComponent,
    DatePipe,
    DecimalPipe
  ],
  templateUrl: './farm-detail.component.html'
})
export class FarmDetailComponent implements OnInit {
  private readonly farmService = inject(FarmService);
  private readonly shedService = inject(ShedService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  branchId = signal<string>('');
  farmId = signal<string>('');
  farm = signal<Farm | null>(null);
  sheds = signal<ShedList[]>([]);
  isLoading = signal<boolean>(false);
  isLoadingSheds = signal<boolean>(false);
  error = signal<string | null>(null);

  statusLabel = computed(() => {
    const s = this.farm()?.status;
    if (s === 1) return 'Active';
    if (s === 2) return 'Inactive';
    if (s === 3) return 'Under Maintenance';
    return 'Closed';
  });

  statusClass = computed(() => {
    const s = this.farm()?.status;
    if (s === 1) return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
    if (s === 2) return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
    if (s === 3) return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
    return 'bg-red-50 text-red-700 dark:bg-red-900/20 dark:text-red-400 border border-red-200 dark:border-red-800';
  });

  farmTypeLabel = computed(() => {
    const t = this.farm()?.type;
    const types: Record<number, string> = {
      1: 'Dairy',
      2: 'Poultry',
      3: 'Mixed',
      4: 'Crop',
      5: 'Aquaculture'
    };
    return t ? (types[t] || 'Unknown') : 'Unknown';
  });

  ngOnInit(): void {
    const branchId = this.route.snapshot.paramMap.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId');
    const farmId = this.route.snapshot.paramMap.get('farmId');

    if (branchId && farmId) {
      this.branchId.set(branchId);
      this.farmId.set(farmId);
      this.loadFarm(farmId);
    } else {
      this.error.set('Branch ID or Farm ID not found in route.');
    }
  }

  loadFarm(id: string): void {
    this.isLoading.set(true);
    this.farmService.getFarmById(id).subscribe({
      next: (data) => {
        this.farm.set(data);
        this.isLoading.set(false);
        this.loadSheds(id);
      },
      error: (err) => {
        this.error.set('Failed to load farm details.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  loadSheds(farmId: string): void {
    this.isLoadingSheds.set(true);
    this.shedService.getShedsByFarm(farmId).subscribe({
      next: (data) => {
        this.sheds.set(data);
        this.isLoadingSheds.set(false);
      },
      error: () => {
        this.isLoadingSheds.set(false);
      }
    });
  }

  onEdit(): void {
    if (this.farm()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'edit']);
    }
  }

  onBack(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
  }

  onManageSheds(): void {
    if (this.farm()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds']);
    }
  }

  deleteFarm(): void {
    if (confirm('Are you sure you want to delete this farm? This action cannot be undone.')) {
      this.farmService.deleteFarm(this.farmId()).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
        },
        error: (err) => {
          this.error.set(err?.error?.detail || 'Failed to delete farm.');
          console.error('Failed to delete farm', err);
        }
      });
    }
  }
}
