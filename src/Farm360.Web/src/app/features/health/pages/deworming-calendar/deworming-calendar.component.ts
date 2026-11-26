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

import { HealthNavComponent } from '../../components/health-nav/health-nav.component';

@Component({
  selector: 'app-deworming-calendar',
  standalone: true,
  imports: [ErrorStateComponent, CommonModule, MatIconModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent, TranslatePipe, HealthNavComponent],
  template: `
<app-page-header
  [title]="'Deworming Calendar' | t"
  [description]="'Deworming doses coming up, from your deworming protocols.' | t">
  <div actions class="flex flex-wrap items-center gap-2">
    @if (events().length) {
      <button type="button" (click)="exportPdf()" [disabled]="isExporting()"
        class="inline-flex items-center gap-1.5 h-10 px-4 text-sm font-semibold rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors disabled:opacity-50">
        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">{{ isExporting() ? 'hourglass_empty' : 'download' }}</mat-icon>
        {{ (isExporting() ? 'Preparing PDF...' : 'Download PDF') | t }}
      </button>
    }
    <button type="button" (click)="openScheduleDialog()"
      class="inline-flex items-center gap-1.5 h-10 px-4 text-sm font-semibold rounded-xl bg-primary-600 hover:bg-primary-700 text-white shadow-sm transition-colors">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> {{ 'Schedule Deworming' | t }}
    </button>
  </div>
</app-page-header>

<app-health-nav></app-health-nav>

<div class="relative min-h-[200px]">
  <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>
  <app-error-state *ngIf="!isLoading() && loadError()" class="block" [message]="loadError()!" (retry)="retryLoad()"></app-error-state>

  <div *ngIf="!isLoading() && !loadError() && events().length === 0">
    <app-empty-state
      icon="event_note"
      [title]="'No deworming planned' | t"
      [description]="'Create a deworming protocol, then apply it to your animals.' | t"
      [actionLabel]="'Schedule Deworming' | t"
      (action)="openScheduleDialog()">
    </app-empty-state>
  </div>

  <div #reportSheet id="dewormingCalendarContainer" *ngIf="!isLoading() && events().length > 0"
       class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
    <div *ngFor="let ev of events()"
         class="bg-white dark:bg-gray-800/80 rounded-2xl border border-gray-200/70 dark:border-gray-700/60 shadow-sm flex flex-col min-w-0">
      <div class="p-4 flex items-start gap-3">
        <div class="w-10 h-10 shrink-0 rounded-xl flex items-center justify-center bg-orange-50 text-orange-600 dark:bg-orange-500/10 dark:text-orange-400">
          <mat-icon class="!w-5 !h-5 !text-[20px]">pest_control</mat-icon>
        </div>
        <div class="min-w-0 flex-1">
          <h3 class="font-semibold text-[15px] leading-snug text-gray-900 dark:text-white break-words">{{ ev.vaccineName }}</h3>
          <p class="mt-0.5 text-xs text-gray-500 dark:text-gray-400 inline-flex items-center gap-1">
            <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">pets</mat-icon>{{ ev.animalTag }}
          </p>
        </div>
        <span class="shrink-0 inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold"
              [ngClass]="{
                'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300': ev.status === 'Completed' || ev.status === 'Administered',
                'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300': ev.status === 'Scheduled',
                'bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-300': ev.status === 'Overdue' || ev.status === 'Missed'
              }">
          {{ ev.status | t }}
        </span>
      </div>
      <dl class="px-4 pb-4 text-sm">
        <dt class="text-[11px] font-medium text-gray-500 dark:text-gray-400">{{ 'Date' | t }}</dt>
        <dd class="font-semibold text-gray-900 dark:text-white">{{ ev.scheduledDate | date:'mediumDate' }}</dd>
      </dl>
    </div>
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
      // A new protocol is not yet applied to any animal: tell the user where to do that.
      if (result) this.notify.success(this.i18n.t('Deworming protocol saved. Open Protocols and apply it to your animals.'));
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
          title: this.i18n.t('Deworming Calendar'),
          subtitle: this.i18n.t('Deworming doses coming up'),
          farmName: farm?.name || this.i18n.t('Primary Farm'),
          orgName: org?.name || 'Farm360',
          currency: 'BDT (৳)',
          metaFields: [
            { label: this.i18n.t('Doses planned'), value: `${list.length}` }
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
