import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IntelligenceService } from '../../services/intelligence.service';
import { FatteningProjectionInputs, ProjectionDefaults, ProfitProjectionResponse } from '../../models/projection.model';
import { ProjectionInputsComponent } from './projection-inputs.component';
import { ProjectionResultsChartComponent } from './projection-results-chart.component';
import { ProjectionDailyTableComponent } from './projection-daily-table.component';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { catchError, filter, switchMap, tap } from 'rxjs/operators';
import { of, Subject } from 'rxjs';
import { toSignal, toObservable } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-cattle-profit-projection',
  standalone: true,
  imports: [
    CommonModule,
    ProjectionInputsComponent,
    ProjectionResultsChartComponent,
    ProjectionDailyTableComponent,
    MatProgressSpinnerModule
  ],
  template: `
    <div class="h-full w-full flex flex-col gap-6 p-6">
      
      <!-- Loading Overlay -->
      <div *ngIf="isLoading()" class="absolute inset-0 z-50 flex items-center justify-center bg-white/50 dark:bg-gray-900/50 backdrop-blur-sm">
        <mat-spinner diameter="48"></mat-spinner>
      </div>

      <!-- Top Section: Inputs -->
      <div class="w-full">
        <app-projection-inputs
          [defaults]="defaultsSignal()"
          (inputsChanged)="onInputsChanged($event)">
        </app-projection-inputs>
      </div>

      <!-- Middle Section: Results & Chart -->
      <div class="w-full flex-grow flex flex-col gap-6">
        <app-projection-results-chart
          *ngIf="resultsSignal()"
          [data]="resultsSignal()">
        </app-projection-results-chart>

        <!-- Bottom Section: Daily Table -->
        <app-projection-daily-table
          *ngIf="resultsSignal()"
          [data]="resultsSignal()">
        </app-projection-daily-table>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CattleProfitProjectionComponent {
  private intelligenceService = inject(IntelligenceService);

  // Animal Id from route or parent component
  animalId = input.required<string>(); 
  isLoading = signal<boolean>(false);
  
  private inputsSubject = new Subject<FatteningProjectionInputs>();

  // Use toSignal for defaults
  defaultsSignal = toSignal(
    toObservable(this.animalId).pipe(
      switchMap(id => this.intelligenceService.getProjectionDefaults(id).pipe(
        catchError(() => of(null)) // Fallback if API fails
      ))
    ),
    { initialValue: null }
  );

  // Declarative pipeline for calculating projections based on input changes
  resultsSignal = toSignal(
    this.inputsSubject.pipe(
      tap(() => this.isLoading.set(true)),
      switchMap(inputs => {
        return this.intelligenceService.calculateProfitProjection(inputs).pipe(
          tap(res => {
            console.log('API Response:', res);
            this.isLoading.set(false);
          }),
          catchError(err => {
            console.error('API Error:', err);
            this.isLoading.set(false);
            return of(null);
          })
        );
      })
    ),
    { initialValue: null }
  );

  constructor() {
    effect(() => {
      // Just a simple effect to clear loading when results arrive
      if (this.resultsSignal() || this.resultsSignal() === null) {
        this.isLoading.set(false);
      }
    }, { allowSignalWrites: true });
  }

  onInputsChanged(inputs: FatteningProjectionInputs) {
    this.inputsSubject.next(inputs);
  }
}
