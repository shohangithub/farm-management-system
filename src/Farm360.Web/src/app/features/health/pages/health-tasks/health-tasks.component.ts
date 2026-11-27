import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, switchMap, tap } from 'rxjs';

import { HealthService } from '../../services/health.service';
import { HealthTaskDto, HealthTasksDto } from '../../models/health.models';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { NotificationService } from '../../../../shared/services/notification.service';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { parseApiError } from '../../../../core/utils/error-parser';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { HealthNavComponent } from '../../components/health-nav/health-nav.component';

type Focus = 'all' | 'overdue' | 'today' | 'tomorrow';
type GroupBy = 'animal' | 'task';

interface TaskGroup {
  key: string;
  title: string;
  subtitle: string | null;
  animalId: string | null;
  items: HealthTaskDto[];
}

/**
 * "Today's tasks": everything due today, what is overdue and tomorrow's work, with the weight-based
 * dose and a stock warning per row. Workers tick tasks done (one or many), skip one with a reason or
 * move it to another day, group by animal or by task, filter by shed and print a checklist.
 * Built for a phone first: one column, 44px+ tap targets, a sticky bar for bulk actions.
 */
@Component({
  selector: 'app-health-tasks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, FormsModule, RouterModule, MatIconModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent,
    ErrorStateComponent, HasPermissionDirective, CanPipe, TranslatePipe, HealthNavComponent,
  ],
  templateUrl: './health-tasks.component.html',
})
export class HealthTasksComponent {
  private readonly health = inject(HealthService);
  private readonly context = inject(WorkingContextService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  private readonly refresh = signal(0);

  readonly focus = signal<Focus>('all');
  readonly groupBy = signal<GroupBy>('animal');
  readonly shedFilter = signal<string>('');
  readonly selected = signal<ReadonlySet<string>>(new Set());

  /** The one card with its skip / move panel open. */
  readonly panel = signal<{ id: string; type: 'skip' | 'move' } | null>(null);
  readonly skipReason = signal('');
  readonly moveDate = signal('');

  readonly todayIso = toLocalIsoDate();
  readonly printedOn = new Date();

  private readonly farm = toSignal(this.context.currentFarm$, { initialValue: null });

  constructor() {
    this.route.queryParamMap.pipe(map(p => p.get('focus'))).subscribe(f => {
      if (f === 'overdue' || f === 'today' || f === 'tomorrow') this.focus.set(f);
    });
  }

  private readonly result = toSignal(
    toObservable(computed(() => ({ farmId: this.farm()?.id ?? '', refresh: this.refresh() }))).pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(({ farmId }) => !farmId
        ? of<HealthTasksDto | null>(null)
        : this.health.getHealthTasks(farmId).pipe(
            catchError(err => {
              this.error.set(parseApiError(err, 'Could not load today\'s tasks.'));
              return of<HealthTasksDto | null>(null);
            }))),
      tap(() => { this.loading.set(false); this.selected.set(new Set()); this.panel.set(null); })
    ),
    { initialValue: null }
  );

  readonly all = computed(() => this.result()?.items ?? []);
  readonly farmName = computed(() => this.farm()?.name ?? '');

  readonly counts = computed(() => {
    const items = this.all();
    return {
      overdue: items.filter(t => t.bucket === 'overdue').length,
      today: items.filter(t => t.bucket === 'today').length,
      tomorrow: items.filter(t => t.bucket === 'tomorrow').length,
    };
  });

  /** Sheds that have tasks, for the filter. */
  readonly sheds = computed(() => {
    const byId = new Map<string, string>();
    for (const t of this.all()) {
      if (t.shedId) byId.set(t.shedId, t.shedName || this.i18n.t('Shed'));
    }
    return [...byId.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });
  readonly hasUnplaced = computed(() => this.all().some(t => !t.shedId));

  readonly visible = computed(() => {
    const focus = this.focus();
    const shed = this.shedFilter();
    return this.all().filter(t =>
      (focus === 'all' || t.bucket === focus) &&
      (!shed || (shed === 'none' ? !t.shedId : t.shedId === shed)));
  });

  readonly groups = computed<TaskGroup[]>(() => {
    const byAnimal = this.groupBy() === 'animal';
    const groups = new Map<string, TaskGroup>();
    for (const t of this.visible()) {
      const key = byAnimal ? t.animalId : t.taskName.toLowerCase();
      let group = groups.get(key);
      if (!group) {
        group = byAnimal
          ? { key, title: t.animalTag, subtitle: this.location(t), animalId: t.animalId, items: [] }
          : { key, title: t.taskName, subtitle: null, animalId: null, items: [] };
        groups.set(key, group);
      }
      group.items.push(t);
    }
    const list = [...groups.values()];
    if (!byAnimal) {
      for (const g of list) g.subtitle = this.i18n.t('{count} animals', { count: new Set(g.items.map(i => i.animalId)).size });
    }
    return list;
  });

  readonly selectedCount = computed(() => this.selected().size);
  readonly allVisibleSelected = computed(() => {
    const v = this.visible().filter(t => t.bucket !== 'tomorrow');
    return v.length > 0 && v.every(t => this.selected().has(t.eventId));
  });

  location(t: HealthTaskDto): string | null {
    if (!t.shedName && !t.penName) return null;
    return [t.shedName, t.penName ? this.i18n.t('Pen {pen}', { pen: t.penName }) : null].filter(Boolean).join(' · ');
  }

  /** "Dose: 2 tablet for 290 kg" (+ "estimated" when the animal was never weighed). */
  doseText(t: HealthTaskDto): string | null {
    if (t.dose == null) return null;
    const unit = t.doseUnit ? this.i18n.t(t.doseUnit) : '';
    const dose = this.formatNumber(t.dose);
    if (t.weightBased && t.doseWeightKg) {
      return this.i18n.t('Dose: {dose} {unit} for {kg} kg', { dose, unit, kg: this.formatNumber(t.doseWeightKg) });
    }
    return this.i18n.t('Dose: {dose} {unit}', { dose, unit });
  }

  stockText(t: HealthTaskDto): string {
    return this.i18n.t('Stock short: {stock} {unit} of {item} left', {
      stock: this.formatNumber(t.stockAvailable ?? 0), unit: t.stockUnit ? this.i18n.t(t.stockUnit) : '', item: t.inventoryItemName ?? '',
    });
  }

  private formatNumber(n: number): string {
    return new Intl.NumberFormat(this.i18n.isBangla ? 'bn-BD' : 'en-US', { maximumFractionDigits: 2 }).format(n);
  }

  setFocus(f: Focus): void {
    this.focus.set(f);
    this.selected.set(new Set());
  }

  isSelected(id: string): boolean {
    return this.selected().has(id);
  }

  toggle(t: HealthTaskDto): void {
    if (t.bucket === 'tomorrow') return; // not due yet
    const next = new Set(this.selected());
    if (next.has(t.eventId)) next.delete(t.eventId); else next.add(t.eventId);
    this.selected.set(next);
  }

  toggleAll(): void {
    const due = this.visible().filter(t => t.bucket !== 'tomorrow');
    this.selected.set(this.allVisibleSelected() ? new Set() : new Set(due.map(t => t.eventId)));
  }

  clearSelection(): void {
    this.selected.set(new Set());
  }

  completeOne(t: HealthTaskDto): void {
    this.complete([t.eventId]);
  }

  completeSelected(): void {
    this.complete([...this.selected()]);
  }

  private complete(ids: string[]): void {
    if (!ids.length || this.saving()) return;
    this.saving.set(true);
    this.health.completeHealthTasks(ids, toLocalIsoDate()).subscribe({
      next: () => {
        this.saving.set(false);
        this.notify.success(ids.length === 1 ? this.i18n.t('Task recorded.') : this.i18n.t('{count} tasks recorded.', { count: ids.length }));
        this.refresh.update(v => v + 1);
      },
      error: err => {
        this.saving.set(false);
        this.notify.error(parseApiError(err, 'Could not record the task.'));
      },
    });
  }

  openPanel(t: HealthTaskDto, type: 'skip' | 'move'): void {
    const open = this.panel();
    if (open?.id === t.eventId && open.type === type) { this.panel.set(null); return; }
    this.skipReason.set('');
    this.moveDate.set(t.scheduledDate < this.todayIso ? this.todayIso : t.scheduledDate);
    this.panel.set({ id: t.eventId, type });
  }

  confirmSkip(t: HealthTaskDto): void {
    const reason = this.skipReason().trim();
    if (!reason) { this.notify.error(this.i18n.t('Please give a reason for skipping.')); return; }
    this.saving.set(true);
    this.health.skipHealthTask(t.eventId, toLocalIsoDate(), reason).subscribe({
      next: () => {
        this.saving.set(false);
        this.notify.success(t.repeatEveryDays
          ? this.i18n.t('Skipped. The next one is due in {days} days.', { days: t.repeatEveryDays })
          : this.i18n.t('Skipped.'));
        this.refresh.update(v => v + 1);
      },
      error: err => { this.saving.set(false); this.notify.error(parseApiError(err, 'Could not skip the task.')); },
    });
  }

  confirmMove(t: HealthTaskDto): void {
    const date = this.moveDate();
    if (!date || date < this.todayIso) { this.notify.error(this.i18n.t('The new date cannot be in the past.')); return; }
    this.saving.set(true);
    this.health.rescheduleHealthTask(t.eventId, date).subscribe({
      next: () => { this.saving.set(false); this.notify.success(this.i18n.t('Task moved.')); this.refresh.update(v => v + 1); },
      error: err => { this.saving.set(false); this.notify.error(parseApiError(err, 'Could not move the task.')); },
    });
  }

  retry(): void {
    this.refresh.update(v => v + 1);
  }

  print(): void {
    window.print();
  }

  trackGroup = (_: number, g: TaskGroup) => g.key;
  trackTask = (_: number, t: HealthTaskDto) => t.eventId;
}
