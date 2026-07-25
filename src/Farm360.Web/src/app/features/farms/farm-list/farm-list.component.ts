import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FarmService } from '../services/farm.service';
import { FarmList } from '../models/farm.model';
import { FarmCardComponent } from '../components/farm-card/farm-card.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-farm-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatIconModule, FarmCardComponent, PageHeaderComponent],
  templateUrl: './farm-list.component.html'
})
export class FarmListComponent implements OnInit {
  private readonly farmService = inject(FarmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  farms = signal<FarmList[]>([]);
  isLoading = signal<boolean>(true);
  branchId = signal<string>('');

  searchTerm = signal<string>('');
  statusFilter = signal<number | null>(null);

  filteredFarms = computed(() => {
    let result = this.farms();
    
    const search = this.searchTerm().toLowerCase();
    if (search) {
      result = result.filter(f => 
        f.farmName.toLowerCase().includes(search) || 
        f.farmCode.toLowerCase().includes(search)
      );
    }
    
    const status = this.statusFilter();
    if (status) {
      result = result.filter(f => f.status === status);
    }
    
    return result;
  });

  ngOnInit(): void {
    const getBranchId = () => this.route.snapshot.paramMap.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId') || '';
    
    const id = getBranchId();
    if (id) {
      this.branchId.set(id);
      this.loadFarms();
    }
    
    this.route.paramMap.subscribe(() => {
      const newId = getBranchId();
      if (newId && newId !== this.branchId()) {
        this.branchId.set(newId);
        this.loadFarms();
      }
    });
  }

  loadFarms(): void {
    this.isLoading.set(true);
    this.farmService.getFarmsByBranch(this.branchId()).subscribe({
      next: (data) => {
        this.farms.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load farms', err);
        this.isLoading.set(false);
      }
    });
  }

  onFilterStatus(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.statusFilter.set(val ? parseInt(val, 10) : null);
  }

  onAddFarm(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', 'new']);
  }
}
