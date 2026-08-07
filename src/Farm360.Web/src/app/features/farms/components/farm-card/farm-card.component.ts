import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { FarmList } from '../../models/farm.model';

@Component({
  selector: 'app-farm-card',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule],
  templateUrl: './farm-card.component.html'
})
export class FarmCardComponent {
  @Input() farm!: FarmList;
  @Input() branchId!: string;

  getFarmTypeName(type: string): string {
    const types: Record<string, string> = {
      'Dairy': 'Dairy',
      'Poultry': 'Poultry',
      'Mixed': 'Mixed',
      'Crop': 'Crop',
      'Aquaculture': 'Aquaculture'
    };
    return types[type] || type || 'Unknown';
  }

  getStatusName(status: string): string {
    const statuses: Record<string, string> = {
      'Active': 'Active',
      'Inactive': 'Inactive',
      'UnderMaintenance': 'Under Maintenance',
      'Closed': 'Closed'
    };
    return statuses[status] || status || 'Unknown';
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Active': return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400';
      case 'Inactive': return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300';
      case 'UnderMaintenance': return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400';
      case 'Closed': return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400';
      default: return 'bg-gray-100 text-gray-800';
    }
  }
}
