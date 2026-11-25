import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { BehaviorSubject, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';

import { WorkingContextService } from '../../../../core/services/working-context.service';
import { DashboardService } from '../../services/dashboard.service';
import { FarmSetupStatus, FarmSetupStep, FarmSetupStepKey } from '../../models/dashboard.model';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { translate } from '../../../../core/i18n/i18n.service';

interface StepInfo {
  title: string;
  description: string;
  icon: string;
  /** Singular / plural noun for the count shown on a finished step. */
  unit: [string, string];
}

interface StepRow extends StepInfo {
  key: FarmSetupStepKey;
  number: number;
  isDone: boolean;
  count: number;
  link: string[];
}

type LoadState =
  | { kind: 'noFarm' }
  | { kind: 'loading'; farmId: string }
  | { kind: 'error'; farmId: string }
  | { kind: 'ready'; farmId: string; status: FarmSetupStatus };

const STEP_INFO: Record<FarmSetupStepKey, StepInfo> = {
  sheds: { title: 'Add your sheds', description: 'Tell us where your animals live, so you can find them by shed.', icon: 'warehouse', unit: ['shed', 'sheds'] },
  breeds: { title: 'Pick your breeds', description: 'Breeds set the growth targets your animals are measured against.', icon: 'category', unit: ['breed', 'breeds'] },
  animals: { title: 'Register your animals', description: 'Add each animal with its tag, breed and shed.', icon: 'pets', unit: ['animal', 'animals'] },
  weights: { title: 'Record a weight', description: 'Weights show how well each animal is growing.', icon: 'monitor_weight', unit: ['weight', 'weights'] },
  inventory: { title: 'Stock your feed store', description: 'Add the feed you have on hand so feeding can draw from it.', icon: 'inventory_2', unit: ['item in stock', 'items in stock'] },
  recipes: { title: 'Create a feed recipe', description: 'Mix your ingredients into a recipe with a known cost per kg.', icon: 'science', unit: ['recipe', 'recipes'] },
  feedChart: { title: 'Set up a feed chart', description: 'Decide how much each animal eats by weight or age.', icon: 'table_chart', unit: ['feed chart', 'feed charts'] },
  plans: { title: 'Start a feeding plan', description: 'Put animals on a feed chart so daily feeding is planned for you.', icon: 'event_note', unit: ['active plan', 'active plans'] },
  accounts: { title: 'Set up your accounts', description: 'Choose your fiscal year so your reports add up correctly.', icon: 'account_balance', unit: ['saved', 'saved'] },
  team: { title: 'Invite your team', description: 'Bring in a manager or worker so they can record work too.', icon: 'group_add', unit: ['member', 'members'] },
};

const STATIC_LINKS: Record<Exclude<FarmSetupStepKey, 'sheds'>, string[]> = {
  breeds: ['/livestock/breeds'],
  animals: ['/livestock/register'],
  weights: ['/livestock'],
  inventory: ['/inventory/items'],
  recipes: ['/feeding/recipes'],
  feedChart: ['/feeding/rules'],
  plans: ['/feeding/board'],
  accounts: ['/finance/accounts-setup'],
  team: ['/settings/team'],
};

const HIDE_KEY_PREFIX = 'farm360.setupChecklist.hidden.';

/**
 * "Get your farm ready": the onboarding checklist at the top of the executive dashboard. Loads its own
 * data for the selected farm, highlights the next step, folds finished steps away and disappears once
 * everything is done (or when the user hides it for this farm).
 */
@Component({
  selector: 'app-setup-checklist',
  standalone: true,
  imports: [RouterModule, MatIconModule, MatButtonModule, TranslatePipe],
  templateUrl: './setup-checklist.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SetupChecklistComponent {
  private readonly contextService = inject(WorkingContextService);
  private readonly dashboardService = inject(DashboardService);

  private readonly retry$ = new BehaviorSubject<void>(undefined);

  private readonly state = toSignal(
    combineLatest([this.contextService.currentFarm$.pipe(map(f => f?.id ?? null)), this.retry$]).pipe(
      switchMap(([farmId]) => farmId
        ? this.dashboardService.getSetupStatus(farmId).pipe(
            map((status): LoadState => ({ kind: 'ready', farmId, status })),
            catchError(() => of<LoadState>({ kind: 'error', farmId })),
            startWith<LoadState>({ kind: 'loading', farmId }))
        : of<LoadState>({ kind: 'noFarm' }))
    ),
    { initialValue: { kind: 'noFarm' } as LoadState }
  );

  private readonly branchId = toSignal(this.contextService.currentBranch$.pipe(map(b => b?.id ?? null)), { initialValue: null });

  /** Farms the user hid the card for during this session; seeded from localStorage per farm. */
  private readonly hiddenFarms = signal<ReadonlySet<string>>(new Set());

  public readonly showCompleted = signal(false);

  public readonly kind = computed(() => this.state().kind);

  private readonly farmId = computed(() => {
    const s = this.state();
    return s.kind === 'noFarm' ? null : s.farmId;
  });

  public readonly isHidden = computed(() => {
    const farmId = this.farmId();
    if (!farmId) return false;
    return this.hiddenFarms().has(farmId) || SetupChecklistComponent.readHidden(farmId);
  });

  public readonly status = computed(() => {
    const s = this.state();
    return s.kind === 'ready' ? s.status : null;
  });

  public readonly rows = computed<StepRow[]>(() => {
    const status = this.status();
    if (!status) return [];
    return status.steps
      .filter(step => step.key in STEP_INFO)
      .map((step, i) => this.toRow(step, i + 1, status.farmId));
  });

  public readonly doneRows = computed(() => this.rows().filter(r => r.isDone));
  public readonly pendingRows = computed(() => this.rows().filter(r => !r.isDone));
  public readonly nextRow = computed(() => this.pendingRows()[0] ?? null);
  public readonly laterRows = computed(() => this.pendingRows().slice(1));

  public readonly doneCount = computed(() => this.doneRows().length);
  public readonly totalCount = computed(() => this.rows().length);
  public readonly percent = computed(() => {
    const total = this.totalCount();
    return total === 0 ? 0 : Math.round((this.doneCount() / total) * 100);
  });

  /** Card is shown unless hidden for this farm, or the farm is fully set up. */
  public readonly visible = computed(() => {
    if (this.isHidden()) return false;
    const status = this.status();
    return !(status && (status.isComplete || this.pendingRows().length === 0));
  });

  public retry(): void {
    this.retry$.next();
  }

  public toggleCompleted(): void {
    this.showCompleted.update(v => !v);
  }

  public hideForNow(): void {
    const farmId = this.farmId();
    if (!farmId) return;
    try {
      localStorage.setItem(HIDE_KEY_PREFIX + farmId, '1');
    } catch {
      // Storage blocked (private mode, quota): the card still hides for this session.
    }
    const next = new Set(this.hiddenFarms());
    next.add(farmId);
    this.hiddenFarms.set(next);
  }

  public countLabel(row: StepRow): string {
    if (row.key === 'accounts') return translate('Saved');
    return translate(`{n} ${row.count === 1 ? row.unit[0] : row.unit[1]}`, { n: row.count });
  }

  private toRow(step: FarmSetupStep, number: number, farmId: string): StepRow {
    return {
      ...STEP_INFO[step.key],
      key: step.key,
      number,
      isDone: step.isDone,
      count: step.count,
      link: this.linkFor(step.key, farmId),
    };
  }

  private linkFor(key: FarmSetupStepKey, farmId: string): string[] {
    if (key !== 'sheds') return STATIC_LINKS[key];
    const branchId = this.branchId();
    // A selected farm always has a selected branch; fall back to the organisation list just in case.
    return branchId
      ? ['/organizations/branches', branchId, 'farms', farmId, 'sheds']
      : ['/organizations'];
  }

  private static readHidden(farmId: string): boolean {
    try {
      return localStorage.getItem(HIDE_KEY_PREFIX + farmId) === '1';
    } catch {
      return false;
    }
  }
}
