import { Component, inject, ChangeDetectionStrategy, signal, computed, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../services/health.service';
import { DewormingCalendarDto } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { MatDialog } from '@angular/material/dialog';
import { CreateProtocolDialogComponent } from '../../components/dialogs/create-protocol-dialog/create-protocol-dialog.component';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-deworming-calendar',
  standalone: true,
  imports: [ErrorStateComponent, CommonModule, MatIconModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent, TranslatePipe],
  template: `
<app-page-header 
  [title]="'Deworming Calendar' | t"
  [description]="'Doses scheduled by deworming protocols. Vaccinations are listed under Due Vaccinations.' | t"
  icon="event_note" 
  iconColor="text-emerald-600">
  <div actions class="flex items-center gap-2">
    <button (click)="exportPdf()" [disabled]="isExporting() || !events()?.length"
      class="px-4 py-2 text-sm font-semibold text-white bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-700 hover:to-teal-700 rounded-lg transition-colors shadow-sm flex items-center gap-1.5 disabled:opacity-50">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">{{ isExporting() ? 'hourglass_empty' : 'picture_as_pdf' }}</mat-icon>
      <span>{{ (isExporting() ? 'Exporting...' : 'Export PDF') | t }}</span>
    </button>
    <button class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm flex items-center gap-1.5" (click)="openScheduleDialog()">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> {{ 'Schedule Deworming' | t }}
    </button>
    <button class="px-4 py-2 text-sm font-semibold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors shadow-sm flex items-center gap-1.5" (click)="loadEvents()">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon> {{ 'Refresh' | t }}
    </button>
  </div>
</app-page-header>

<div #reportSheet id="dewormingCalendarContainer" class="bg-white/80 dark:bg-surface-dark/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
  <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>
  <app-error-state *ngIf="!isLoading() && loadError()" class="block p-6" [message]="loadError()!" (retry)="retryLoad()"></app-error-state>

  <div class="relative overflow-x-auto">
    <table class="w-full text-sm text-left" *ngIf="events().length > 0">
      <thead class="text-xs text-gray-700 uppercase bg-gray-50 border-b border-gray-100 dark:bg-gray-800 dark:text-gray-400 dark:border-gray-700 font-bold tracking-wider">
        <tr>
          <th scope="col" class="px-6 py-4">{{ 'Date' | t }}</th>
          <th scope="col" class="px-6 py-4">{{ 'Animal' | t }}</th>
          <th scope="col" class="px-6 py-4">{{ 'Vaccine / Medicine' | t }}</th>
          <th scope="col" class="px-6 py-4">{{ 'Status' | t }}</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let ev of events()" class="border-b border-gray-50 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
          <td class="px-6 py-4 text-gray-600 dark:text-gray-400">
            {{ ev.scheduledDate | date:'mediumDate' }}
          </td>
          <td class="px-6 py-4 font-medium text-gray-900 dark:text-white">
            {{ ev.animalTag }}
          </td>
          <td class="px-6 py-4 text-gray-700 dark:text-gray-300">
            {{ ev.vaccineName }}
          </td>
          <td class="px-6 py-4">
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium" 
                  [ngClass]="{
                    'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400': ev.status === 'Completed' || ev.status === 'Administered',
                    'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-400': ev.status === 'Scheduled',
                    'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400': ev.status === 'Overdue' || ev.status === 'Missed'
                  }">
              {{ ev.status | t }}
            </span>
          </td>
        </tr>
      </tbody>
    </table>
    
    <app-empty-state 
      *ngIf="!isLoading() && events().length === 0 && !loadError()"
      icon="event_note"
      [title]="'No Deworming Events' | t"
      [description]="'There are no deworming events scheduled for this farm.' | t"
      [actionLabel]="'Refresh' | t"
      (action)="loadEvents()">
    </app-empty-state>
  </div>
</div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DewormingCalendarComponent {
  private readonly notify = inject(NotificationService);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private pdfExportService = inject(PdfExportService);
  private dialog = inject(MatDialog);
  private readonly i18n = inject(I18nService);
  
  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  isLoading = signal(true);
  readonly loadError = signal<string | null>(null);

  retryLoad(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  private refreshTrigger = signal(0);
  private currentFarmId = toSignal(this.contextService.currentFarm$, { initialValue: null });

  private fetchParams = computed(() => ({
    farmId: this.currentFarmId()?.id || '',
    refresh: this.refreshTrigger()
  }));

  private eventsResult = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => { this.isLoading.set(true); this.loadError.set(null); }),
      switchMap(({ farmId }) => {
        if (!farmId) return of({ items: [], totalCount: 0 });
        return this.healthService.getDewormingCalendar(farmId).pipe(
          catchError(err => {
            this.loadError.set(parseApiError(err, 'Could not load the deworming calendar.'));
            return of({ items: [], totalCount: 0 });
          })
        );
      }),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [], totalCount: 0 } }
  );

  events = computed(() => this.eventsResult().items);

  loadEvents() {
    this.refreshTrigger.update(v => v + 1);
  }

  openScheduleDialog() {
    const dialogRef = this.dialog.open(CreateProtocolDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '800px',
      data: {
        title: 'Deworming: ',
        isDeworming: true,
        lockIsDeworming: true,
        steps: [{
          stepName: 'First Dose',
          targetAgeDays: 0,
          vaccineName: '',
          dosageInstruction: ''
        }]
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      // NOTE: Creating protocol doesn't automatically assign it.
      // After protocol creation, the user will need to assign it.
      // We could add a prompt here to navigate to protocol list to assign it.
    });
  }

  async exportPdf(): Promise<void> {
    const el = this.reportSheet?.nativeElement;
    if (!el) return;

    const list = this.events() || [];
    const farm = this.contextService.currentFarmValue;
    const org = this.contextService.currentOrgValue;

    this.isExporting.set(true);
    try {
      await this.pdfExportService.exportElement(el, {
        filename: `Farm360_Deworming_Calendar_${toLocalIsoDate()}`,
        orientation: 'landscape',
        header: {
          title: this.i18n.t('Deworming & Parasite Control Schedule'),
          subtitle: this.i18n.t('Doses scheduled by deworming protocols'),
          farmName: farm?.name || this.i18n.t('Primary Farm'),
          orgName: org?.name || 'Farm360 Enterprise',
          currency: 'BDT (৳)',
          metaFields: [
            { label: this.i18n.t('Total Scheduled Events'), value: `${list.length}` },
            { label: this.i18n.t('Calendar Scope'), value: this.i18n.t('Active Herd Schedule') }
          ]
        },
        showSignatures: true
      });
    } catch (err) {
      this.notify.error(parseApiError(err, 'Could not export the PDF. Please try again.'));
    } finally {
      this.isExporting.set(false);
    }
  }
}
