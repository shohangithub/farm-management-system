import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, combineLatest, of, switchMap, tap } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { FeedingService } from '../../services/feeding.service';
import { BoardAnimal, FeedingBoard, RationItem } from '../../models/feeding.models';
import { FeedingSubnavComponent } from '../../components/feeding-subnav/feeding-subnav.component';
import { AssignFeedingPlanDialogComponent, SetRationDialogData } from '../../components/dialogs/assign-feeding-plan-dialog/assign-feeding-plan-dialog.component';

type Filter = 'all' | 'unfed' | 'extras' | 'paused';

/**
 * Who eats what: one row per animal with every ration feeding it today (feed chart, extras,
 * shed/batch plans), its kg and taka a day, and the actions to change it.
 */
@Component({
  selector: 'app-feeding-board',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, PageHeaderComponent, FeedingSubnavComponent],
  template: `
    <app-page-header title="Who Eats What" description="Every animal and what it is fed each day — set, change, pause or stop rations here."
      breadcrumbActiveNode="Who Eats What">
      <div actions>
        <button mat-flat-button (click)="openSetRation()" class="!rounded-xl !bg-emerald-600 !text-white">
          <mat-icon>restaurant</mat-icon> Set ration{{ selected().length ? ' (' + selected().length + ')' : '' }}
        </button>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-7xl">
      <app-feeding-subnav></app-feeding-subnav>

      <div *ngIf="error()" class="mb-4 p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-800 text-sm text-rose-700 dark:text-rose-300">{{ error() }}</div>

      <ng-container *ngIf="board() as b">
        <!-- Old schedules -->
        <div *ngIf="b.summary.activeLegacySchedules > 0" class="mb-4 p-4 rounded-2xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 flex items-start gap-3">
          <mat-icon class="text-amber-600 shrink-0">update</mat-icon>
          <div class="flex-1 text-xs text-amber-900 dark:text-amber-100">
            <b>{{ b.summary.activeLegacySchedules }} old feeding schedule(s)</b> are still switched on. Schedules only describe feeding —
            they never created daily feed, used stock or charged cost. Convert them to shed/batch rations so they do.
          </div>
          <button mat-flat-button (click)="convertSchedules()" [disabled]="busy()" class="!rounded-xl !bg-amber-600 !text-white !text-xs">Convert now</button>
        </div>

        <!-- Summary -->
        <div class="grid grid-cols-2 lg:grid-cols-5 gap-3 mb-4">
          <button (click)="filter.set('all')" class="text-left p-4 rounded-2xl bg-white/80 dark:bg-gray-800/80 border" [ngClass]="filter() === 'all' ? 'border-emerald-400' : 'border-gray-100 dark:border-gray-800'">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">Animals</p>
            <h3 class="text-xl font-black mt-1 mb-0">{{ b.summary.animals }}</h3>
          </button>
          <button (click)="filter.set('unfed')" class="text-left p-4 rounded-2xl border" [ngClass]="[filter() === 'unfed' ? 'border-rose-400' : 'border-gray-100 dark:border-gray-800', b.summary.unfed ? 'bg-rose-50 dark:bg-rose-950/30' : 'bg-white/80 dark:bg-gray-800/80']">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">No ration</p>
            <h3 class="text-xl font-black mt-1 mb-0" [class.text-rose-600]="b.summary.unfed">{{ b.summary.unfed }}</h3>
          </button>
          <button (click)="filter.set('extras')" class="text-left p-4 rounded-2xl bg-white/80 dark:bg-gray-800/80 border" [ngClass]="filter() === 'extras' ? 'border-amber-400' : 'border-gray-100 dark:border-gray-800'">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">With extras</p>
            <h3 class="text-xl font-black mt-1 mb-0">{{ b.summary.withExtras }}</h3>
          </button>
          <div class="p-4 rounded-2xl bg-white/80 dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">Feed a day</p>
            <h3 class="text-xl font-black mt-1 mb-0">{{ b.summary.totalKgPerDay | number:'1.0-1' }} kg</h3>
          </div>
          <div class="p-4 rounded-2xl bg-white/80 dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">Cost a day</p>
            <h3 class="text-xl font-black mt-1 mb-0 text-emerald-600">৳{{ b.summary.totalCostPerDayBdt | number:'1.0-0' }}</h3>
          </div>
        </div>

        <!-- Filters -->
        <div class="flex flex-wrap items-center gap-2 mb-3 text-xs">
          <input [ngModel]="search()" (ngModelChange)="search.set($event)" placeholder="Search tag…" class="px-3 py-2 rounded-xl border border-gray-300 dark:border-gray-700 bg-white dark:bg-gray-900 w-48" />
          <select [ngModel]="shed()" (ngModelChange)="shed.set($event)" class="px-3 py-2 rounded-xl border border-gray-300 dark:border-gray-700 bg-white dark:bg-gray-900">
            <option [ngValue]="null">All sheds</option>
            <option *ngFor="let s of shedOptions()" [ngValue]="s">{{ s }}</option>
          </select>
          <button (click)="filter.set('paused')" class="px-3 py-2 rounded-xl border" [ngClass]="filter() === 'paused' ? 'border-sky-400 bg-sky-50 dark:bg-sky-950/30' : 'border-gray-300 dark:border-gray-700'">Paused ({{ b.summary.paused }})</button>
          <span class="ml-auto text-gray-500">{{ rows().length }} shown · {{ selected().length }} selected</span>
          <button *ngIf="selected().length" (click)="selected.set([])" class="text-gray-500 underline">clear</button>
        </div>

        <!-- Table -->
        <div class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-800/50 overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
              <tr>
                <th class="px-3 py-2 w-8"><input type="checkbox" class="accent-emerald-600" [checked]="allSelected()" (change)="toggleAll()" /></th>
                <th class="text-left px-3 py-2">Animal</th>
                <th class="text-left px-3 py-2">Where</th>
                <th class="text-left px-3 py-2">Eats every day</th>
                <th class="text-right px-3 py-2">kg/day</th>
                <th class="text-right px-3 py-2">৳/day</th>
                <th class="px-3 py-2"></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngIf="!rows().length"><td colspan="7" class="px-4 py-10 text-center text-gray-400">No animals match.</td></tr>
              <tr *ngFor="let a of rows(); trackBy: trackAnimal" class="border-t border-gray-100 dark:border-gray-700/60 align-top"
                  [ngClass]="{ 'bg-rose-50/50 dark:bg-rose-950/10': !hasFeed(a) }">
                <td class="px-3 py-2.5"><input type="checkbox" class="accent-emerald-600" [checked]="isSelected(a)" (change)="toggle(a)" /></td>
                <td class="px-3 py-2.5">
                  <a [routerLink]="['/livestock', a.animalId]" class="font-semibold text-gray-900 dark:text-white hover:text-emerald-600">{{ a.tag }}</a>
                  <span class="block text-[11px] text-gray-500">{{ a.weightKg ? (a.weightKg | number:'1.0-1') + ' kg' : 'no weight' }}<span *ngIf="a.ageDays !== null && a.ageDays !== undefined"> · {{ ageLabel(a.ageDays!) }}</span></span>
                </td>
                <td class="px-3 py-2.5 text-xs text-gray-600 dark:text-gray-300">
                  {{ a.shedName || '—' }}<span *ngIf="a.penName"> / {{ a.penName }}</span>
                  <span *ngIf="a.batchName" class="block text-[11px] text-gray-400">{{ a.batchName }}</span>
                </td>
                <td class="px-3 py-2.5">
                  <div *ngIf="!a.items.length" class="text-xs font-semibold text-rose-600">No ration — not fed by the system</div>
                  <div *ngFor="let it of a.items" class="flex flex-wrap items-center gap-x-2 text-xs py-0.5" [class.opacity-50]="it.isPaused">
                    <span class="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase" [ngClass]="kindClass(it)">{{ kindLabel(it) }}</span>
                    <span class="font-semibold">{{ it.name }}</span>
                    <span class="text-gray-500">{{ it.kgPerDay | number:'1.0-3' }} kg<span *ngIf="it.feedName && it.feedName !== it.name"> · {{ it.feedName }}</span></span>
                    <span *ngIf="it.fit === 'BelowRange' || it.fit === 'AboveRange'" class="text-amber-600" title="Outside the chart's ranges — the nearest range is used">⚠ {{ it.fit === 'BelowRange' ? 'below' : 'above' }} chart</span>
                    <span *ngIf="it.isPaused" class="text-sky-600">paused{{ it.pausedUntil ? ' until ' + (it.pausedUntil | date:'d MMM') : '' }}</span>
                    <span *ngIf="it.planId && it.kind !== 'Group'" class="ml-auto inline-flex gap-0.5">
                      <button (click)="pause(a, it)" title="Pause" class="p-0.5 text-gray-400 hover:text-sky-600"><mat-icon class="!text-[16px] !w-4 !h-4">pause_circle</mat-icon></button>
                      <button (click)="stop(a, it)" title="Stop" class="p-0.5 text-gray-400 hover:text-rose-600"><mat-icon class="!text-[16px] !w-4 !h-4">stop_circle</mat-icon></button>
                    </span>
                  </div>
                </td>
                <td class="px-3 py-2.5 text-right font-mono">{{ a.totalKgPerDay | number:'1.0-2' }}</td>
                <td class="px-3 py-2.5 text-right font-mono">{{ a.totalCostPerDayBdt | number:'1.0-0' }}</td>
                <td class="px-3 py-2.5 text-right whitespace-nowrap">
                  <button (click)="openSetRation([a.animalId], hasChart(a) ? 'switch' : undefined)" [title]="hasChart(a) ? 'Change chart' : 'Set ration'" class="p-1 text-gray-400 hover:text-emerald-600">
                    <mat-icon class="!text-[18px]">{{ hasChart(a) ? 'swap_horiz' : 'restaurant' }}</mat-icon>
                  </button>
                  <button (click)="openSetRation([a.animalId], 'extra')" title="Add extra" class="p-1 text-gray-400 hover:text-amber-600">
                    <mat-icon class="!text-[18px]">add_circle</mat-icon>
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <p class="text-[11px] text-gray-500 mt-2">
          <b>Chart</b> = amount by weight or age · <b>Extra</b> = fixed add-on for this animal · <b>Group</b> = shed/pen/batch ration, per head.
          Today's quantities are confirmed on <a routerLink="/feeding/today" class="underline">Today</a>.
          Stopped and past plans: <a routerLink="/feeding/plans" class="underline">plan history</a>.
        </p>
      </ng-container>

      <!-- Pause dialog -->
      <div *ngIf="pausing() as p" class="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" (click)="pausing.set(null)">
        <div class="bg-white dark:bg-gray-800 rounded-2xl p-5 w-full max-w-sm space-y-3 text-xs" (click)="$event.stopPropagation()">
          <h3 class="text-sm font-black m-0">Pause {{ p.item.name }} for {{ p.animal.tag }}</h3>
          <label class="flex flex-col gap-1 font-semibold">From <input type="date" [(ngModel)]="p.from" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-transparent" /></label>
          <label class="flex flex-col gap-1 font-semibold">Feed again on <input type="date" [(ngModel)]="p.until" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-transparent" /></label>
          <label class="flex flex-col gap-1 font-semibold">Reason <input [(ngModel)]="p.reason" placeholder="e.g. sick, off feed" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-transparent" /></label>
          <div class="flex justify-end gap-2 pt-1">
            <button mat-stroked-button (click)="pausing.set(null)" class="!rounded-xl">Cancel</button>
            <button mat-flat-button (click)="confirmPause()" [disabled]="busy() || !p.from || !p.until || p.until <= p.from" class="!rounded-xl !bg-sky-600 !text-white">Pause</button>
          </div>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FeedingBoardComponent {
  private readonly feeding = inject(FeedingService);
  private readonly context = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly board = signal<FeedingBoard | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  readonly filter = signal<Filter>('all');
  readonly search = signal('');
  readonly shed = signal<string | null>(null);
  readonly selected = signal<string[]>([]);
  readonly pausing = signal<{ animal: BoardAnimal; item: RationItem; from: string; until: string; reason: string } | null>(null);

  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  readonly shedOptions = computed(() =>
    [...new Set((this.board()?.animals ?? []).map(a => a.shedName).filter((s): s is string => !!s))].sort());

  readonly rows = computed(() => {
    const term = this.search().trim().toLowerCase();
    return (this.board()?.animals ?? [])
      .filter(a => !term || a.tag.toLowerCase().includes(term))
      .filter(a => !this.shed() || a.shedName === this.shed())
      .filter(a => {
        switch (this.filter()) {
          case 'unfed': return !this.hasFeed(a);
          case 'extras': return a.items.some(i => i.kind === 'Extra');
          case 'paused': return a.items.some(i => i.isPaused);
          default: return true;
        }
      });
  });

  readonly allSelected = computed(() => this.rows().length > 0 && this.rows().every(r => this.selected().includes(r.animalId)));

  constructor() {
    combineLatest([this.refresh$, this.context.currentFarm$]).pipe(
      tap(() => this.error.set(null)),
      switchMap(([, farm]) => !farm ? of(null) : this.feeding.getBoard(farm.id).pipe(
        catchError(err => { this.error.set(err.error?.detail || 'Failed to load the feeding board.'); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(b => this.board.set(b));
  }

  hasFeed(a: BoardAnimal): boolean {
    return a.items.some(i => !i.isPaused);
  }

  hasChart(a: BoardAnimal): boolean {
    return a.items.some(i => i.kind === 'Chart');
  }

  kindLabel(it: RationItem): string {
    return ({ Chart: 'Chart', Extra: 'Extra', Group: 'Group' } as Record<string, string>)[it.kind] ?? it.kind;
  }

  kindClass(it: RationItem): string {
    return it.kind === 'Chart' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/50 dark:text-emerald-300'
      : it.kind === 'Extra' ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/50 dark:text-amber-300'
      : 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900/50 dark:text-indigo-300';
  }

  ageLabel(days: number): string {
    return days >= 730 ? `${Math.floor(days / 365)} yr` : days >= 60 ? `${Math.floor(days / 30)} mo` : `${days} d`;
  }

  trackAnimal = (_: number, a: BoardAnimal) => a.animalId;

  isSelected(a: BoardAnimal): boolean {
    return this.selected().includes(a.animalId);
  }

  toggle(a: BoardAnimal): void {
    this.selected.update(s => s.includes(a.animalId) ? s.filter(x => x !== a.animalId) : [...s, a.animalId]);
  }

  toggleAll(): void {
    const ids = this.rows().map(r => r.animalId);
    this.selected.set(this.allSelected() ? this.selected().filter(id => !ids.includes(id)) : [...new Set([...this.selected(), ...ids])]);
  }

  openSetRation(animalIds?: string[], mode?: 'extra' | 'switch'): void {
    const ids = animalIds ?? this.selected();
    const data: SetRationDialogData = { animalIds: ids.length ? ids : undefined, mode };
    this.dialog.open(AssignFeedingPlanDialogComponent, { disableClose: true, width: '95vw', maxWidth: '860px', data })
      .afterClosed().subscribe(saved => {
        if (saved) {
          this.selected.set([]);
          this.refresh$.next();
        }
      });
  }

  pause(animal: BoardAnimal, item: RationItem): void {
    const today = new Date().toISOString().split('T')[0];
    const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0];
    this.pausing.set({ animal, item, from: today, until: tomorrow, reason: '' });
  }

  confirmPause(): void {
    const p = this.pausing();
    if (!p?.item.planId) return;
    this.busy.set(true);
    this.feeding.excludeAnimalFromPlan(p.item.planId, p.from, p.reason || 'Paused', p.until).subscribe({
      next: () => { this.busy.set(false); this.pausing.set(null); this.snack.open('Paused', 'Close', { duration: 2500 }); this.refresh$.next(); },
      error: err => { this.busy.set(false); this.error.set(err.error?.detail || 'Could not pause.'); }
    });
  }

  stop(animal: BoardAnimal, item: RationItem): void {
    if (!item.planId || !confirm(`Stop feeding "${item.name}" to ${animal.tag}? Past feeding records are kept.`)) return;
    this.busy.set(true);
    this.feeding.cancelPlan(item.planId).subscribe({
      next: () => { this.busy.set(false); this.snack.open('Stopped', 'Close', { duration: 2500 }); this.refresh$.next(); },
      error: err => { this.busy.set(false); this.error.set(err.error?.detail || 'Could not stop the ration.'); }
    });
  }

  convertSchedules(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId || !confirm('Convert all active feeding schedules into shed/batch rations? They will start feeding from today.')) return;
    this.busy.set(true);
    this.feeding.convertSchedules(farmId).subscribe({
      next: r => {
        this.busy.set(false);
        alert(`${r.converted} converted, ${r.skipped} skipped.\n\n${r.notes.join('\n')}`);
        this.refresh$.next();
      },
      error: err => { this.busy.set(false); this.error.set(err.error?.detail || 'Conversion failed.'); }
    });
  }
}
