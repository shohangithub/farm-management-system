import { Component, inject, ChangeDetectionStrategy, signal, computed } from '@angular/core';
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
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-health-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatDialogModule, PageHeaderComponent, LoadingComponent],
  templateUrl: './health-dashboard.html',
  styleUrls: ['./health-dashboard.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HealthDashboardComponent {
  private healthService = inject(HealthService);
  private dialog = inject(MatDialog);
  private contextService = inject(WorkingContextService);

  // --- Reactive State (Signals) ---
  isLoading = signal(true);
  error = signal('');
  private refreshTrigger = signal(0);

  // Derive farm context as a signal
  private currentFarm = toSignal(this.contextService.currentFarm$);

  // Combined trigger for loading data
  private fetchTrigger = computed(() => ({
    farm: this.currentFarm(),
    refresh: this.refreshTrigger()
  }));

  // Reactive Data Stream
  dashboardData = toSignal(
    toObservable(this.fetchTrigger).pipe(
      tap(() => {
        this.isLoading.set(true);
        this.error.set('');
      }),
      switchMap(({ farm }) => {
        if (!farm) {
          this.isLoading.set(false);
          return of(null);
        }
        return this.healthService.getHealthDashboard().pipe(
          catchError((err) => {
            console.error('Error loading health dashboard', err);
            this.error.set('Failed to load dashboard data. Please try again.');
            return of(null);
          }),
          tap(() => this.isLoading.set(false))
        );
      })
    ),
    { initialValue: null }
  );

  loadDashboard(): void {
    // Triggers reactivity naturally without manual CDR
    this.refreshTrigger.update(v => v + 1);
  }

  // --- Dialogs ---
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
