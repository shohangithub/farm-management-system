import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FeedingService } from '../../services/feeding.service';
import { FeedFormula } from '../../models/feeding.models';
import { CreateFormulaDialogComponent } from '../../components/dialogs/create-formula-dialog/create-formula-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-formula-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Feed Formulas & Ration Builder"
      description="Formulate custom rations with calculated crude protein, dry matter, and metabolizable energy profiles."
      breadcrumbActiveNode="Ration Formulas">
      <div actions>
        <button (click)="openCreateFormulaDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Build New Formula
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Search Toolbar -->
      <div class="p-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-900/30">
        <div class="relative w-full sm:w-80">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
          <input [(ngModel)]="searchTerm" (ngModelChange)="onSearchChange()"
            placeholder="Search formulas by title..."
            class="w-full pl-9 pr-4 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
        </div>
      </div>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && formulas().length === 0"
        icon="science"
        title="No formulas configured"
        description="Build your first custom feed formula to start assigning rations."
        actionLabel="Build Formula"
        (action)="openCreateFormulaDialog()">
      </app-empty-state>

      <!-- Formula Cards Grid -->
      <div *ngIf="!isLoading() && formulas().length > 0" class="p-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        @for (formula of formulas(); track formula.id) {
          <div class="group relative bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-xl transition-all duration-300 overflow-hidden transform hover:-translate-y-1 flex flex-col justify-between">
            <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-teal-500/5 rotate-[-10deg] pointer-events-none transition-transform duration-500 group-hover:scale-110">science</mat-icon>

            <!-- Card Header -->
            <div class="p-5 flex items-start justify-between border-b border-gray-50 dark:border-gray-700/50 relative z-10">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-teal-500 to-cyan-600 text-white flex items-center justify-center shadow-md shadow-teal-500/20 group-hover:scale-110 transition-transform duration-300">
                  <mat-icon class="!w-5 !h-5 !text-[20px]">science</mat-icon>
                </div>
                <div>
                  <h3 class="font-bold text-gray-900 dark:text-white text-base leading-tight group-hover:text-teal-600 transition-colors">{{ formula.title }}</h3>
                  <span class="inline-flex items-center mt-1 text-xs font-semibold text-gray-500 dark:text-gray-400">
                    {{ formula.targetSpeciesName }} {{ formula.targetStage ? '• ' + formula.targetStage : '' }}
                  </span>
                </div>
              </div>

              <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
                {{ formula.statusName }}
              </span>
            </div>

            <!-- Card Body -->
            <div class="p-5 flex-1 relative z-10">
              <div class="grid grid-cols-3 gap-2 p-3 bg-gray-50/80 dark:bg-gray-900/50 rounded-xl text-center border border-gray-100 dark:border-gray-800 mb-4">
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">DM %</div>
                  <div class="font-bold text-gray-900 dark:text-white text-sm mt-0.5">{{ formula.dryMatterPct }}%</div>
                </div>
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">CP %</div>
                  <div class="font-bold text-emerald-600 dark:text-emerald-400 text-sm mt-0.5">{{ formula.crudeProteinPct }}%</div>
                </div>
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">Cost / kg</div>
                  <div class="font-bold text-gray-900 dark:text-white text-sm mt-0.5">৳ {{ formula.totalCostPerKgBdt }}</div>
                </div>
              </div>

              <div class="space-y-1.5">
                <div class="text-xs font-bold text-gray-400 uppercase tracking-wider">Ingredients ({{ formula.ingredients.length }}):</div>
                @for (ing of formula.ingredients; track ing.id) {
                  <div class="flex items-center justify-between text-xs text-gray-600 dark:text-gray-300">
                    <span>{{ ing.ingredientName }}</span>
                    <span class="font-semibold text-gray-900 dark:text-white">{{ ing.percentage }}%</span>
                  </div>
                }
              </div>
            </div>

            <!-- Footer Action -->
            <div class="p-3 bg-gray-50/80 dark:bg-gray-800/80 border-t border-gray-100 dark:border-gray-700/50 flex items-center justify-between relative z-10">
              <span class="text-xs text-gray-400 font-medium">ME: {{ formula.metabolizableEnergyMjPerKg }} MJ/kg</span>
              <button (click)="openEditDialog(formula)" class="px-3 py-1.5 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:bg-white dark:hover:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-700 transition-colors shadow-sm inline-flex items-center gap-1">
                <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">edit</mat-icon> Edit Formula
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FormulaListComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);

  readonly isLoading = signal(true);
  readonly formulas = signal<FeedFormula[]>([]);

  searchTerm = '';

  ngOnInit(): void {
    this.loadFormulas();
  }

  loadFormulas(): void {
    this.isLoading.set(true);
    this.feedingService.getFormulas(1, 50, this.searchTerm).subscribe({
      next: (res) => {
        this.formulas.set(res.items);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  onSearchChange(): void {
    this.loadFormulas();
  }

  openCreateFormulaDialog(): void {
    const dialogRef = this.dialog.open(CreateFormulaDialogComponent, { disableClose: true, width: '700px' });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadFormulas();
    });
  }

  openEditDialog(formula: FeedFormula): void {
    const dialogRef = this.dialog.open(CreateFormulaDialogComponent, {
      disableClose: true,
      width: '700px',
      data: formula
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadFormulas();
    });
  }
}
