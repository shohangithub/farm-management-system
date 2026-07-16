import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FarmList } from '../../models/farm.model';

@Component({
  selector: 'app-farm-card',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './farm-card.component.html'
})
export class FarmCardComponent {
  @Input() farm!: FarmList;
  @Input() branchId!: string;

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
