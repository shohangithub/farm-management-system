import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { PenService } from '../services/pen.service';
import { Pen } from '../models/pen.model';

@Component({
  selector: 'app-pen-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './pen-detail.component.html'
})
export class PenDetailComponent implements OnInit {
  private penService = inject(PenService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  pen: Pen | null = null;
  isLoading = true;
  penId: string = '';
  shedId: string = '';
  farmId: string = '';
  branchId: string = '';
  Math = Math;

  ngOnInit(): void {
    this.branchId = this.route.snapshot.paramMap.get('branchId') || '';
    this.farmId = this.route.snapshot.paramMap.get('farmId') || '';
    this.shedId = this.route.snapshot.paramMap.get('shedId') || '';
    this.penId = this.route.snapshot.paramMap.get('penId') || '';
    
    if (this.penId) {
      this.loadPen();
    }
  }

  loadPen(): void {
    this.isLoading = true;
    this.penService.getPenById(this.penId).subscribe({
      next: (data) => {
        this.pen = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error(err);
        this.isLoading = false;
      }
    });
  }

  deletePen(): void {
    if (confirm('Are you sure you want to delete this pen? This action cannot be undone.')) {
      this.penService.deletePen(this.penId).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId, 'farms', this.farmId, 'sheds', this.shedId]);
        },
        error: (err) => {
          console.error('Failed to delete pen', err);
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
