import { RouterLink } from '@angular/router';
import { moneyAccountName, registerSourceText } from '../../models/finance.model';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, combineLatest, of, switchMap, tap } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { FinanceService } from '../../services/finance.service';
import { AccountingHealth, LedgerRepairResult } from '../../models/finance.model';
import { ConfirmService } from '../../../../shared/services/confirm.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { I18nService } from '../../../../core/i18n/i18n.service';

/**
 * Accounting Health — proves the books. Each balance-sheet account is reconciled against the
 * register that should back it (stock on hand, the herd, the loan register, the investor register),
 * open issues are listed, and records made before the accounting rules existed can be repaired.
 */
@Component({
  selector: 'app-accounting-health',
  standalone: true,
  imports: [FinanceSubnavComponent, TranslatePipe, MoneyPipe, RouterLink, CommonModule, FormsModule, MatIconModule, MatButtonModule, PageHeaderComponent, DatePipe],
  templateUrl: './accounting-health.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingHealthComponent {
  readonly accountName = moneyAccountName;
  readonly registerSource = registerSourceText;

  private readonly confirmService = inject(ConfirmService);
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
  readonly isBangla = this.i18n.isBangla;

  readonly health = signal<AccountingHealth | null>(null);
  readonly isLoading = signal(true);
  readonly isRepairing = signal(false);
  readonly error = signal<string | null>(null);
  readonly repair = signal<LedgerRepairResult | null>(null);
  readonly adjustInventory = signal(false);

  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  readonly fixable = computed(() => (this.health()?.issues ?? []).filter(i => i.canAutoFix));
  readonly errors = computed(() => (this.health()?.issues ?? []).filter(i => i.severity === 'Error').length);
  readonly inventoryDifference = computed(() =>
    this.health()?.reconciliations.find(r => r.key === 'inventory' && !r.isReconciled) ?? null);

  constructor() {
    combineLatest([this.refresh$, this.context.currentFarm$])
      .pipe(
        tap(() => {
          this.isLoading.set(true);
          this.error.set(null);
        }),
        switchMap(([, farm]) => {
          if (!farm) return of(null);
          return this.finance.getAccountingHealth(farm.id).pipe(
            catchError(err => {
              this.error.set(parseApiError(err, this.i18n.t('Failed to check the books.')));
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.health.set(result);
        this.isLoading.set(false);
      });
  }

  recheck(): void {
    this.repair.set(null);
    this.refresh$.next();
  }

  async runRepair(): Promise<void> {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;

    if (this.adjustInventory() && !(await this.confirmService.confirmAsync({ message: this.i18n.t('Adjust the books to the stock on hand?\n\nDo this only after a physical stock count. Any difference is posted as opening stock (if you have more than the books) or as shrinkage (if you have less).'), confirmText: this.i18n.t('Adjust') }))) {
      return;
    }

    this.isRepairing.set(true);
    this.error.set(null);
    this.finance.repairLedger(farmId, this.adjustInventory()).subscribe({
      next: result => {
        this.repair.set(result);
        this.health.set(result.healthAfter);
        this.adjustInventory.set(false);
        this.isRepairing.set(false);
      },
      error: err => {
        this.error.set(parseApiError(err, this.i18n.t('Ledger repair failed.')));
        this.isRepairing.set(false);
      },
    });
  }

  abs(value: number): number {
    return Math.abs(value);
  }
}
