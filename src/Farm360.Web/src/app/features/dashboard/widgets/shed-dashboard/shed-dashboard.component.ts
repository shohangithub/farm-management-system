import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { ShedService } from '../../services/shed.service';
import { ShedList } from '../../models/shed.model';

@Component({
  selector: 'app-shed-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './shed-dashboard.component.html'
})
export class ShedDashboardComponent implements OnInit {
  private shedService = inject(ShedService);
  private route = inject(ActivatedRoute);

  sheds: ShedList[] = [];
  farmId: string = '';
  branchId: string = '';
  
  totalSheds = 0;
  totalOccupancy = 0;
  totalCapacity = 0;
  Math = Math;

  ngOnInit(): void {
    this.route.parent?.paramMap.subscribe(params => {
      this.branchId = params.get('branchId') || '';
      this.farmId = params.get('farmId') || '';
      if (this.farmId) {
        this.loadStats();
      }
    });
  }

  loadStats(): void {
    this.shedService.getShedsByFarm(this.farmId).subscribe({
      next: (data) => {
        this.sheds = data;
        this.totalSheds = data.length;
        this.totalOccupancy = data.reduce((acc, s) => acc + (s.currentOccupancy || 0), 0);
        this.totalCapacity = data.reduce((acc, s) => acc + (s.capacity || 0), 0);
      }
    });
  }
}
