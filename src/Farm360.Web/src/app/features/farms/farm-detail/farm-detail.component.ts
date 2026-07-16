import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FarmService } from '../services/farm.service';
import { Farm } from '../models/farm.model';

@Component({
  selector: 'app-farm-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './farm-detail.component.html'
})
export class FarmDetailComponent implements OnInit {
  private farmService = inject(FarmService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  farm: Farm | null = null;
  isLoading = true;
  farmId: string = '';
  branchId: string = '';

  ngOnInit(): void {
    this.branchId = this.route.snapshot.paramMap.get('branchId') || '';
    this.farmId = this.route.snapshot.paramMap.get('farmId') || '';
    
    if (this.farmId) {
      this.loadFarm();
    }
  }

  loadFarm(): void {
    this.isLoading = true;
    this.farmService.getFarmById(this.farmId).subscribe({
      next: (data) => {
        this.farm = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error(err);
        this.isLoading = false;
      }
    });
  }

  deleteFarm(): void {
    if (confirm('Are you sure you want to delete this farm? This action cannot be undone.')) {
      this.farmService.deleteFarm(this.farmId).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId, 'farms']);
        },
        error: (err) => {
          console.error('Failed to delete farm', err);
        }
      });
    }
  }

  getFarmTypeName(type: number): string {
    const types: Record<number, string> = {
      1: 'Dairy',
      2: 'Poultry',
      3: 'Mixed',
      4: 'Crop',
      5: 'Aquaculture'
    };
    return types[type] || 'Unknown';
  }

  getStatusName(status: number): string {
    const statuses: Record<number, string> = {
      1: 'Active',
      2: 'Inactive',
      3: 'Under Maintenance',
      4: 'Closed'
    };
    return statuses[status] || 'Unknown';
  }

  getStatusClass(status: number): string {
    switch (status) {
      case 1: return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400';
      case 2: return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300';
      case 3: return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400';
      case 4: return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400';
      default: return 'bg-gray-100 text-gray-800';
    }
  }
}
