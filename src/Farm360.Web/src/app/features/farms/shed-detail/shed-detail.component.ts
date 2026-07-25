import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';
import { ShedService } from '../services/shed.service';
import { Shed } from '../models/shed.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-shed-detail',
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
  templateUrl: './shed-detail.component.html'
})
export class ShedDetailComponent implements OnInit {
  private readonly shedService = inject(ShedService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  branchId = signal<string>('');
  farmId = signal<string>('');
  shedId = signal<string>('');
  shed = signal<Shed | null>(null);
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);

  statusLabel = computed(() => {
    const s = this.shed()?.status;
    if (s === 1) return 'Active';
    if (s === 2) return 'Inactive';
    if (s === 3) return 'Maintenance';
    return 'Unknown';
  });

  statusClass = computed(() => {
    const s = this.shed()?.status;
    if (s === 1) return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
    if (s === 2) return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
    if (s === 3) return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
    return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
  });

  ngOnInit(): void {
    const branchId = this.route.snapshot.paramMap.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId');
    const farmId = this.route.snapshot.paramMap.get('farmId') || this.route.parent?.snapshot.paramMap.get('farmId');
    const shedId = this.route.snapshot.paramMap.get('shedId');

    if (branchId && farmId && shedId) {
      this.branchId.set(branchId);
      this.farmId.set(farmId);
      this.shedId.set(shedId);
      this.loadShed(shedId);
    } else {
      this.error.set('Required IDs not found in route.');
    }
  }

  loadShed(id: string): void {
    this.isLoading.set(true);
    this.shedService.getShedById(id).subscribe({
      next: (data) => {
        this.shed.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load shed details.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  onEdit(): void {
    if (this.shed()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'edit']);
    }
  }

  onBack(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds']);
  }

  onManagePens(): void {
    if (this.shed()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens']);
    }
  }

  deleteShed(): void {
    if (confirm('Are you sure you want to delete this shed? This action cannot be undone.')) {
      this.shedService.deleteShed(this.shedId()).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds']);
        },
        error: (err) => {
          this.error.set(err?.error?.detail || 'Failed to delete shed.');
          console.error('Failed to delete shed', err);
        }
      });
    }
  }
}
