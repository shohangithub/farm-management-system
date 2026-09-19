import { Component, inject, ChangeDetectionStrategy, signal, computed, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { HealthService } from '../../services/health.service';
import { VaccinationEventDto, VaccinationStatus } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ScheduleVaccinationDialog } from '../../components/dialogs/schedule-vaccination-dialog/schedule-vaccination-dialog.component';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';

@Component({
  selector: 'app-vaccination-due-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatIconModule, MatButtonModule, MatDialogModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent],
  templateUrl: './vaccination-due-list.component.html',
  styleUrls: ['./vaccination-due-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VaccinationDueListComponent {
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private pdfExportService = inject(PdfExportService);
  private dialog = inject(MatDialog);

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  readonly VaccinationStatus = VaccinationStatus;

  // State
  isLoading = signal(true);
  error = signal('');
  private refreshTrigger = signal(0);

  // Derive target date (30 days ahead)
  private dateStr = computed(() => {
    const d = new Date();
    d.setDate(d.getDate() + 30);
    return d.toISOString().split('T')[0];
  });

  // Current farm ID from context
  private currentFarmId = toSignal(this.contextService.currentFarm$, { initialValue: null });

  // Combined params for the fetch request
  private fetchParams = computed(() => ({
    farmId: this.currentFarmId()?.id || '11111111-1111-1111-1111-111111111111', // Fallback for MVP
    dateStr: this.dateStr(),
    refresh: this.refreshTrigger()
  }));

  // Reactive data stream
  upcomingVaccinations = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => { this.isLoading.set(true); this.error.set(''); }),
      switchMap(({ farmId, dateStr }) =>
        this.healthService.getUpcomingVaccinations(farmId, dateStr).pipe(
          catchError((err) => {
            console.error(err);
            this.error.set('Failed to load upcoming vaccinations');
            return of([]);
          }),
          tap(() => this.isLoading.set(false))
        )
      )
    ),
    { initialValue: [] as VaccinationEventDto[] }
  );

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
          this.refreshTrigger.update(v => v + 1);
        },
        error: (err) => {
          console.error('Failed to administer', err);
        }
      });
  }

  openRecordVaccinationDialog(): void {
    const dialogRef = this.dialog.open(ScheduleVaccinationDialog, {
      width: '720px',
      panelClass: 'custom-dialog-container',
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.refreshTrigger.update(v => v + 1);
      }
    });
  }

  async exportPdf(): Promise<void> {
    const el = this.reportSheet?.nativeElement;
    if (!el) return;

    const list = this.upcomingVaccinations() || [];
    const farm = this.contextService.currentFarmValue;
    const org = this.contextService.currentOrgValue;

    this.isExporting.set(true);
    try {
      await this.pdfExportService.exportElement(el, {
        filename: `Farm360_Vaccination_Schedule_${new Date().toISOString().split('T')[0]}`,
        orientation: 'landscape',
        header: {
          title: 'Herd Vaccination Schedule & Due Report',
          subtitle: 'Upcoming Immunization Records (Next 30 Days)',
          farmName: farm?.name || 'Primary Farm',
          orgName: org?.name || 'Farm360 Enterprise',
          currency: 'BDT (৳)',
          metaFields: [
            { label: 'Total Scheduled Events', value: `${list.length}` },
            { label: 'Status Window', value: 'Next 30 Days' }
          ]
        },
        showSignatures: true
      });
    } catch (err) {
      console.error('Failed to export Vaccination Due PDF:', err);
    } finally {
      this.isExporting.set(false);
    }
  }
}
