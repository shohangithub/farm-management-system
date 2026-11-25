import { Component, ChangeDetectionStrategy, inject, signal, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { IntelligenceService } from '../../services/intelligence.service';
import { FatteningProjectionInputs, ProjectionDefaults, ProfitProjectionResponse } from '../../models/projection.model';
import { ProjectionInputsComponent } from './projection-inputs.component';
import { ProjectionResultsChartComponent } from './projection-results-chart.component';
import { ProjectionDailyTableComponent } from './projection-daily-table.component';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { catchError, debounceTime, filter, finalize, map, switchMap, tap } from 'rxjs/operators';
import { of, Subject } from 'rxjs';
import { toSignal, toObservable } from '@angular/core/rxjs-interop';
import { I18nService } from '../../../../core/i18n/i18n.service';

/** Generic assumptions (the reference spreadsheet's) used only if the animal's defaults can't be loaded. */
function fallbackDefaults(): ProjectionDefaults {
  const v = (value: number) => ({ value, source: 'SystemDefault', sourceLabel: 'Generic assumption — animal defaults unavailable' });
  return {
    startingLiveWeightKg: v(200),
    purchasePriceBdt: v(0),
    currentMeatPriceBdtPerKg: v(680),
    initialMeatYieldRatio: v(0.5),
    dailyLiveWeightGainKg: v(0.7),
    meatYieldOnDailyGainRatio: v(0.5),
    dailyFeedQuantityKgAtStart: v(3),
    feedPriceBdtPerKg: v(45),
    dailyGrassCostBdt: v(20),
    dailyOtherCostBdt: v(30),
    monthlyLaborCostBdt: v(1500),
    fatteningPeriodDays: v(120)
  };
}

@Component({
  selector: 'app-cattle-profit-projection',
  standalone: true,
  imports: [
    CommonModule,
    ProjectionInputsComponent,
    ProjectionResultsChartComponent,
    ProjectionDailyTableComponent,
    MatProgressSpinnerModule,
    MatIconModule
  ],
  template: `
    <div class="relative h-full w-full flex flex-col gap-6 p-6">

      <div *ngIf="defaultsWarning()" class="flex items-center gap-2 text-sm rounded-xl px-4 py-3 bg-amber-50 text-amber-800 border border-amber-200 dark:bg-amber-900/20 dark:text-amber-300 dark:border-amber-800">
        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">warning</mat-icon>
        {{ defaultsWarning() }}
      </div>

      <!-- Top Section: Inputs -->
      <div class="w-full">
        <app-projection-inputs
          [defaults]="defaultsSignal()"
          (inputsChanged)="onInputsChanged($event)">
        </app-projection-inputs>
      </div>

      <div *ngIf="calcError()" class="flex items-center gap-2 text-sm rounded-xl px-4 py-3 bg-red-50 text-red-700 border border-red-200 dark:bg-red-900/20 dark:text-red-300 dark:border-red-800">
        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">error</mat-icon>
        {{ calcError() }}
      </div>

      <div *ngIf="!resultsSignal() && isLoading()" class="flex justify-center py-12">
        <mat-spinner diameter="40"></mat-spinner>
      </div>

      <!-- Middle Section: Results & Chart (dimmed, not hidden, while recalculating) -->
      <div class="relative w-full flex-grow flex flex-col gap-6 transition-opacity" *ngIf="resultsSignal() as results"
           [class.opacity-60]="isLoading()">
        <div *ngIf="isLoading()" class="absolute top-3 right-3 z-20"><mat-spinner diameter="24"></mat-spinner></div>
        <app-projection-results-chart [data]="results"></app-projection-results-chart>

        <!-- Bottom Section: Daily Table -->
        <app-projection-daily-table [data]="results"></app-projection-daily-table>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CattleProfitProjectionComponent {
  private intelligenceService = inject(IntelligenceService);
  private readonly i18n = inject(I18nService);

  animalId = input.required<string>();
  isLoading = signal<boolean>(false);
  calcError = signal<string | null>(null);
  defaultsWarning = signal<string | null>(null);

  private inputsSubject = new Subject<FatteningProjectionInputs>();

  defaultsSignal = toSignal(
    toObservable(this.animalId).pipe(
      tap(() => this.defaultsWarning.set(null)),
      switchMap(id => this.intelligenceService.getProjectionDefaults(id).pipe(
        catchError(() => {
          this.defaultsWarning.set(this.i18n.t("Could not load this animal's figures — showing generic assumptions. Enter its weight and cost to date manually."));
          return of(fallbackDefaults());
        })
      ))
    ),
    { initialValue: null }
  );

  /**
   * Last successful projection. Kept on screen while a recalculation runs or if one fails, so the
   * results don't vanish on every keystroke or on a single bad request.
   */
  resultsSignal = toSignal(
    this.inputsSubject.pipe(
      debounceTime(350),
      // switchMap cancels (and finalizes) the previous request before calling this, so the
      // loading flag is raised after the old request's finalize has cleared it.
      switchMap(inputs => {
        this.isLoading.set(true);
        this.calcError.set(null);
        return this.intelligenceService.calculateProfitProjection(inputs).pipe(
          map(res => res as ProfitProjectionResponse | null),
          catchError((err: HttpErrorResponse) => {
            this.calcError.set(err?.error?.detail || err?.error?.title || this.i18n.t('Projection could not be calculated. Check the inputs and try again.'));
            return of(null);
          }),
          finalize(() => this.isLoading.set(false))
        );
      }),
      // Drop failed calls so the previous result stays visible.
      filter((res): res is ProfitProjectionResponse => !!res)
    ),
    { initialValue: null }
  );

  onInputsChanged(inputs: FatteningProjectionInputs) {
    this.inputsSubject.next(inputs);
  }
}
