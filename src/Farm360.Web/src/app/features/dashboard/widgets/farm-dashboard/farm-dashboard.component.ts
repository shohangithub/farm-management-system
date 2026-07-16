import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { FarmService } from '../../../farms/services/farm.service';
import { FarmList } from '../../../farms/models/farm.model';

@Component({
  selector: 'app-farm-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './farm-dashboard.component.html'
})
export class FarmDashboardComponent implements OnInit {
  private farmService = inject(FarmService);
  private route = inject(ActivatedRoute);

  farms: FarmList[] = [];
  branchId: string = '';
  totalFarms = 0;
  totalAnimals = 0;
  totalCapacity = 0;

  ngOnInit(): void {
    this.route.parent?.paramMap.subscribe(params => {
      this.branchId = params.get('branchId') || '';
      if (this.branchId) {
        this.loadStats();
      }
    });
  }

  loadStats(): void {
    this.farmService.getFarmsByBranch(this.branchId).subscribe({
      next: (data) => {
        this.farms = data;
        this.totalFarms = data.length;
        this.totalAnimals = data.reduce((acc, f) => acc + (f.currentAnimalCount || 0), 0);
        this.totalCapacity = data.reduce((acc, f) => acc + (f.capacity || 0), 0);
      }
    });
  }
}
