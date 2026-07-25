import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';
import { PenService } from '../services/pen.service';
import { Pen } from '../models/pen.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-pen-detail',
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
  templateUrl: './pen-detail.component.html'
})
export class PenDetailComponent implements OnInit {
  private readonly penService = inject(PenService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  branchId = signal<string>('');
  farmId = signal<string>('');
  shedId = signal<string>('');
  penId = signal<string>('');
  pen = signal<Pen | null>(null);
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);

  statusLabel = computed(() => {
    const s = this.pen()?.status;
    if (s === 1) return 'Active';
    if (s === 2) return 'Inactive';
    if (s === 3) return 'Maintenance';
    return 'Unknown';
  });

  statusClass = computed(() => {
    const s = this.pen()?.status;
    if (s === 1) return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
    if (s === 2) return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
    if (s === 3) return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
    return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
  });

  ngOnInit(): void {
    // The route parameters are deep, we might need to get them from parent routes
    let currentRoute: ActivatedRoute | null = this.route;
    let branchId = '';
    let farmId = '';
    let shedId = '';
    const penId = this.route.snapshot.paramMap.get('penId');

    while (currentRoute) {
      if (!branchId) branchId = currentRoute.snapshot.paramMap.get('branchId') || '';
      if (!farmId) farmId = currentRoute.snapshot.paramMap.get('farmId') || '';
      if (!shedId) shedId = currentRoute.snapshot.paramMap.get('shedId') || '';
      currentRoute = currentRoute.parent;
    }

    if (branchId && farmId && shedId && penId) {
      this.branchId.set(branchId);
      this.farmId.set(farmId);
      this.shedId.set(shedId);
      this.penId.set(penId);
      this.loadPen(penId);
    } else {
      this.error.set('Required IDs not found in route.');
    }
  }

  loadPen(id: string): void {
    this.isLoading.set(true);
    this.penService.getPenById(id).subscribe({
      next: (data) => {
        this.pen.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load pen details.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  onEdit(): void {
    if (this.pen()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens', this.penId(), 'edit']);
    }
  }

  onBack(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens']);
  }

  deletePen(): void {
    if (confirm('Are you sure you want to delete this pen? This action cannot be undone.')) {
      this.penService.deletePen(this.penId()).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens']);
        },
        error: (err) => {
          this.error.set(err?.error?.detail || 'Failed to delete pen.');
          console.error('Failed to delete pen', err);
        }
      });
    }
  }
}
