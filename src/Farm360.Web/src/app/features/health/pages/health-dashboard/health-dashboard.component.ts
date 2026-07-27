import { Component, inject, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HealthService } from '../../services/health.service';
import { HealthDashboardDto } from '../../models/health.models';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ScheduleVaccinationDialog } from '../../components/dialogs/schedule-vaccination-dialog/schedule-vaccination-dialog.component';
import { LogTreatmentDialog } from '../../components/dialogs/log-treatment-dialog/log-treatment-dialog.component';
import { RecordMortalityDialog } from '../../components/dialogs/record-mortality-dialog/record-mortality-dialog.component';

@Component({
  selector: 'app-health-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatDialogModule],
  templateUrl: './health-dashboard.html',
  styleUrls: ['./health-dashboard.scss']
})
export class HealthDashboardComponent implements OnInit {
  private healthService = inject(HealthService);
  private dialog = inject(MatDialog);
  private cdr = inject(ChangeDetectorRef);

  dashboardData: HealthDashboardDto | null = null;
  isLoading = true;
  error = '';

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.isLoading = true;
    this.error = '';
    this.healthService.getHealthDashboard().subscribe({
      next: (data) => {
        this.dashboardData = data;
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error loading health dashboard', err);
        this.error = 'Failed to load dashboard data. Please try again.';
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  openScheduleVaccinationDialog(): void {
    const dialogRef = this.dialog.open(ScheduleVaccinationDialog, { width: '500px' });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadDashboard();
    });
  }

  openLogTreatmentDialog(): void {
    const dialogRef = this.dialog.open(LogTreatmentDialog, { width: '600px' });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadDashboard();
    });
  }

  openRecordMortalityDialog(): void {
    const dialogRef = this.dialog.open(RecordMortalityDialog, { width: '500px' });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadDashboard();
    });
  }
}
