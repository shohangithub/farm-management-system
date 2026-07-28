import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../services/health.service';
import { VaccinationEventDto, VaccinationStatus } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-vaccination-due-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatIconModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent],
  templateUrl: './vaccination-due-list.component.html',
  styleUrls: ['./vaccination-due-list.component.scss']
})
export class VaccinationDueListComponent implements OnInit {
  private healthService = inject(HealthService);

  upcomingVaccinations: VaccinationEventDto[] = [];
  loading = false;
  error = '';
  readonly VaccinationStatus = VaccinationStatus;
  
  // Dummy farm ID for MVP, would normally come from ContextService
  selectedFarmId = '11111111-1111-1111-1111-111111111111';

  ngOnInit(): void {
    this.loadUpcomingVaccinations();
  }

  loadUpcomingVaccinations(): void {
    this.loading = true;
    this.error = '';
    // Look ahead 30 days
    const beforeDate = new Date();
    beforeDate.setDate(beforeDate.getDate() + 30);
    const dateStr = beforeDate.toISOString().split('T')[0];

    this.healthService.getUpcomingVaccinations(this.selectedFarmId, dateStr)
      .subscribe({
        next: (events) => {
          this.upcomingVaccinations = events;
          this.loading = false;
        },
        error: (err) => {
          console.error(err);
          this.error = 'Failed to load upcoming vaccinations';
          this.loading = false;
        }
      });
  }

  getUrgencyClass(dateStr: string): string {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const dueDate = new Date(dateStr);
    dueDate.setHours(0, 0, 0, 0);

    const diffTime = dueDate.getTime() - today.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    if (diffDays < 0) return 'danger-row'; // Overdue
    if (diffDays === 0) return 'warning-row'; // Today
    if (diffDays <= 7) return 'info-row'; // This week
    return '';
  }

  administer(id: string): void {
    const todayStr = new Date().toISOString().split('T')[0];
    this.healthService.administerVaccination(id, todayStr, 'Administered routinely')
      .subscribe({
        next: () => {
          this.loadUpcomingVaccinations();
        },
        error: (err) => {
          console.error('Failed to administer', err);
        }
      });
  }
}
