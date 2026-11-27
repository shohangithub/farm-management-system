import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap } from 'rxjs';
import { HealthService } from '../../services/health.service';
import { HealthTasksDto } from '../../models/health.models';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

/** Dashboard card: how many health tasks are overdue, due today and tomorrow, linking to "Today's tasks". */
@Component({
  selector: 'app-health-tasks-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, MatIconModule, TranslatePipe],
  template: `
    @if (data(); as d) {
      <section aria-labelledby="health-tasks-heading"
        class="bg-white dark:bg-gray-800/80 rounded-2xl border shadow-sm overflow-hidden"
        [ngClass]="overdue() > 0 ? 'border-rose-200 dark:border-rose-800/60' : 'border-gray-200/70 dark:border-gray-700/60'">
        <div class="px-4 py-3 flex items-center gap-3 border-b border-gray-100 dark:border-gray-700/60">
          <div class="w-10 h-10 rounded-xl flex items-center justify-center bg-primary-50 text-primary-600 dark:bg-primary-500/10 dark:text-primary-300">
            <mat-icon class="!w-5 !h-5 !text-[20px]">task_alt</mat-icon>
          </div>
          <h2 id="health-tasks-heading" class="text-base font-bold text-gray-900 dark:text-white m-0 flex-1">{{ "Today's health tasks" | t }}</h2>
          <a routerLink="/health/today" class="inline-flex items-center gap-1 h-11 px-3 rounded-xl text-sm font-semibold text-primary-700 dark:text-primary-300 hover:bg-primary-50 dark:hover:bg-primary-500/10">
            {{ 'Open' | t }} <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
          </a>
        </div>
        <div class="grid grid-cols-3 divide-x divide-gray-100 dark:divide-gray-700/60">
          <a routerLink="/health/today" [queryParams]="{ focus: 'overdue' }" class="p-4 hover:bg-gray-50 dark:hover:bg-gray-700/40">
            <span class="block text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{{ 'Overdue' | t }}</span>
            <span class="block text-2xl font-bold" [ngClass]="overdue() > 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ overdue() }}</span>
          </a>
          <a routerLink="/health/today" [queryParams]="{ focus: 'today' }" class="p-4 hover:bg-gray-50 dark:hover:bg-gray-700/40">
            <span class="block text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{{ 'Today' | t }}</span>
            <span class="block text-2xl font-bold text-gray-900 dark:text-white">{{ todayCount() }}</span>
          </a>
          <a routerLink="/health/today" [queryParams]="{ focus: 'tomorrow' }" class="p-4 hover:bg-gray-50 dark:hover:bg-gray-700/40">
            <span class="block text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{{ 'Tomorrow' | t }}</span>
            <span class="block text-2xl font-bold text-gray-900 dark:text-white">{{ tomorrow() }}</span>
          </a>
        </div>
      </section>
    }
  `,
})
export class HealthTasksCardComponent {
  private readonly health = inject(HealthService);
  private readonly context = inject(WorkingContextService);
  private readonly farm = toSignal(this.context.currentFarm$, { initialValue: null });

  readonly data = toSignal(
    toObservable(computed(() => this.farm()?.id ?? '')).pipe(
      switchMap(farmId => farmId
        ? this.health.getHealthTasks(farmId).pipe(catchError(() => of<HealthTasksDto | null>(null)))
        : of<HealthTasksDto | null>(null))),
    { initialValue: null });

  readonly overdue = computed(() => this.data()?.items.filter(t => t.bucket === 'overdue').length ?? 0);
  readonly todayCount = computed(() => this.data()?.items.filter(t => t.bucket === 'today').length ?? 0);
  readonly tomorrow = computed(() => this.data()?.items.filter(t => t.bucket === 'tomorrow').length ?? 0);
}
