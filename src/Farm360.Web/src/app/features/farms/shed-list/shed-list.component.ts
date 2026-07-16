import { Component, OnInit, inject, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { ShedService } from '../services/shed.service';
import { ShedList } from '../models/shed.model';

@Component({
  selector: 'app-shed-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './shed-list.component.html'
})
export class ShedListComponent implements OnInit {
  private shedService = inject(ShedService);
  private route = inject(ActivatedRoute);

  sheds: ShedList[] = [];
  isLoading = true;
  farmId: string = '';
  branchId: string = '';
  Math = Math;

  ngOnInit(): void {
    // Expected to be a child route of farm
    this.route.parent?.paramMap.subscribe(params => {
      this.farmId = params.get('farmId') || '';
      this.branchId = params.get('branchId') || '';
      if (this.farmId) {
        this.loadSheds();
      }
    });
  }

  loadSheds(): void {
    this.isLoading = true;
    this.shedService.getShedsByFarm(this.farmId).subscribe({
      next: (data) => {
        this.sheds = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Failed to load sheds', err);
        this.isLoading = false;
      }
    });
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
      case 1: return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400';
      case 2: return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300';
      case 3: return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400';
      default: return 'bg-gray-100 text-gray-800';
    }
  }
}
