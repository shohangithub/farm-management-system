import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { FarmService } from '../services/farm.service';
import { FarmList } from '../models/farm.model';
import { FarmCardComponent } from '../components/farm-card/farm-card.component';

@Component({
  selector: 'app-farm-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FarmCardComponent],
  templateUrl: './farm-list.component.html'
})
export class FarmListComponent implements OnInit {
  private farmService = inject(FarmService);
  private route = inject(ActivatedRoute);

  farms: FarmList[] = [];
  isLoading = true;
  branchId: string = '';

  ngOnInit(): void {
    // Expecting to be a child route of branch, so parent params have branchId
    this.route.parent?.paramMap.subscribe(params => {
      this.branchId = params.get('branchId') || '';
      if (this.branchId) {
        this.loadFarms();
      }
    });
  }

  loadFarms(): void {
    this.isLoading = true;
    this.farmService.getFarmsByBranch(this.branchId).subscribe({
      next: (data) => {
        this.farms = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Failed to load farms', err);
        this.isLoading = false;
      }
    });
  }
}
