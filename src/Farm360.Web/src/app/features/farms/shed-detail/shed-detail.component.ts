import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { ShedService } from '../../services/shed.service';
import { Shed } from '../../models/shed.model';

@Component({
  selector: 'app-shed-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './shed-detail.component.html'
})
export class ShedDetailComponent implements OnInit {
  private shedService = inject(ShedService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  shed: Shed | null = null;
  isLoading = true;
  shedId: string = '';
  farmId: string = '';
  branchId: string = '';
  Math = Math;

  ngOnInit(): void {
    this.branchId = this.route.snapshot.paramMap.get('branchId') || '';
    this.farmId = this.route.snapshot.paramMap.get('farmId') || '';
    this.shedId = this.route.snapshot.paramMap.get('shedId') || '';
    
    if (this.shedId) {
      this.loadShed();
    }
  }

  loadShed(): void {
    this.isLoading = true;
    this.shedService.getShedById(this.shedId).subscribe({
      next: (data) => {
        this.shed = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error(err);
        this.isLoading = false;
      }
    });
  }

  deleteShed(): void {
    if (confirm('Are you sure you want to delete this shed? This action cannot be undone.')) {
      this.shedService.deleteShed(this.shedId).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId, 'farms', this.farmId]);
        },
        error: (err) => {
          console.error('Failed to delete shed', err);
        }
      });
    }
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
