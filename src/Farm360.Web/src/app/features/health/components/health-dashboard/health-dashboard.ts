import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, switchMap, tap } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { NgxEchartsDirective } from 'ngx-echarts';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AnalyticsService, HealthAnalyticsDto } from '../../../../core/services/analytics.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';

@Component({
  selector: 'app-health-dashboard',
  standalone: true,
  imports: [
    CommonModule, 
    MatIconModule, 
    MatButtonModule, 
    NgxEchartsDirective,
    PageHeaderComponent, 
    LoadingComponent, 
    EmptyStateComponent
  ],
  templateUrl: './health-dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HealthDashboardComponent {
  private analyticsService = inject(AnalyticsService);
  private workingContext = inject(WorkingContextService);
  private router = inject(Router);

  /** The farm picked in the context selector; the page re-loads whenever it changes. */
  private farm = toSignal(this.workingContext.currentFarm$, { initialValue: this.workingContext.currentFarmValue });
  public hasFarm = computed(() => !!this.farm()?.id);

  public isLoading = signal(false);
  public loadFailed = signal(false);
  private data = signal<HealthAnalyticsDto | null>(null);

  public hasData = computed(() => !!this.data());

  public chartOptions = computed(() => {
    const data = this.data();
    if (!data) return {};

    return {
      tooltip: {
        trigger: 'item',
        formatter: '{a} <br/>{b}: {c}%'
      },
      legend: {
        orient: 'vertical',
        left: 'left'
      },
      color: ['#10b981', '#f59e0b'],
      series: [
        {
          name: 'Vaccination Compliance',
          type: 'pie',
          radius: ['40%', '70%'],
          avoidLabelOverlap: false,
          itemStyle: {
            borderRadius: 10,
            borderColor: '#fff',
            borderWidth: 2
          },
          label: {
            show: false,
            position: 'center'
          },
          emphasis: {
            label: {
              show: true,
              fontSize: '20',
              fontWeight: 'bold',
              formatter: '{d}%'
            }
          },
          labelLine: {
            show: false
          },
          data: [
            { value: data.vaccinationCompliancePercentage, name: 'Vaccinated (Last 6 Mos)' },
            { value: 100 - data.vaccinationCompliancePercentage, name: 'Not Vaccinated / Overdue' }
          ]
        }
      ]
    };
  });

  constructor() {
    this.workingContext.currentFarm$.pipe(
      map(f => f?.id ?? null),
      distinctUntilChanged(),
      tap(id => {
        this.data.set(null);
        this.loadFailed.set(false);
        this.isLoading.set(!!id);
      }),
      switchMap(id => !id ? of(null) : this.analyticsService.getHealthAnalytics(id).pipe(
        catchError(() => {
          this.loadFailed.set(true);
          return of(null);
        })
      )),
      takeUntilDestroyed()
    ).subscribe(result => {
      this.data.set(result);
      this.isLoading.set(false);
    });
  }

  public printReport(): void {
    window.print();
  }

  public goToHealthDashboard(): void {
    this.router.navigate(['/health']);
  }

  public get dataValue(): HealthAnalyticsDto | undefined {
    return this.data() ?? undefined;
  }
}
