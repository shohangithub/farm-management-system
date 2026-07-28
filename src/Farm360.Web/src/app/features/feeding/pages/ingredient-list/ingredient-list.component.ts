import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FeedingService } from '../../services/feeding.service';
import { FeedCategory, FeedCategoryNames, FeedIngredient } from '../../models/feeding.models';
import { CreateIngredientDialogComponent } from '../../components/dialogs/create-ingredient-dialog/create-ingredient-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-ingredient-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Feed Ingredients Catalog"
      description="Manage raw feed ingredients, dry matter, crude protein content, and unit costs."
      breadcrumbActiveNode="Ingredients Catalog">
      <div actions>
        <button (click)="openCreateIngredientDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Add New Ingredient
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Filters Toolbar -->
      <div class="p-4 border-b border-gray-100 dark:border-gray-800 flex flex-col sm:flex-row items-center gap-4 bg-gray-50/50 dark:bg-gray-900/30">
        <div class="relative w-full sm:w-72">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
          <input [(ngModel)]="searchTerm" (ngModelChange)="applyFilter()"
            placeholder="Search ingredients..."
            class="w-full pl-9 pr-4 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
        </div>

        <select [(ngModel)]="selectedCategory" (ngModelChange)="applyFilter()"
          class="w-full sm:w-56 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option [ngValue]="null">All Categories</option>
          @for (cat of categoryOptions; track cat.value) {
            <option [ngValue]="cat.value">{{ cat.label }}</option>
          }
        </select>
      </div>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && filteredIngredients().length === 0"
        icon="restaurant_menu"
        title="No ingredients found"
        description="Add a raw ingredient to build custom feed formulas."
        actionLabel="Add Ingredient"
        (action)="openCreateIngredientDialog()">
      </app-empty-state>

      <!-- Ingredients Grid -->
      <div *ngIf="!isLoading() && filteredIngredients().length > 0" class="p-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        @for (ing of filteredIngredients(); track ing.id) {
          <div class="group relative bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-xl transition-all duration-300 overflow-hidden transform hover:-translate-y-1 flex flex-col">
            <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none transition-transform duration-500 group-hover:scale-110">restaurant</mat-icon>

            <!-- Card Header -->
            <div class="p-5 flex items-start justify-between border-b border-gray-50 dark:border-gray-700/50 relative z-10">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 group-hover:scale-110 transition-transform duration-300">
                  <mat-icon class="!w-5 !h-5 !text-[20px]">restaurant</mat-icon>
                </div>
                <div>
                  <h3 class="font-bold text-gray-900 dark:text-white text-base leading-tight group-hover:text-emerald-600 transition-colors">{{ ing.name }}</h3>
                  <span class="inline-flex items-center mt-1 text-xs font-medium text-gray-500 dark:text-gray-400">
                    {{ ing.categoryName }}
                  </span>
                </div>
              </div>

              <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-sm"
                [ngClass]="ing.isPreloaded ? 'bg-blue-50 text-blue-700 border border-blue-200' : 'bg-emerald-50 text-emerald-700 border border-emerald-200'">
                {{ ing.isPreloaded ? 'Preloaded' : 'Custom' }}
              </span>
            </div>

            <!-- Card Body -->
            <div class="p-5 flex-1 relative z-10">
              <div class="grid grid-cols-3 gap-2 p-3 bg-gray-50/80 dark:bg-gray-900/50 rounded-xl text-center border border-gray-100 dark:border-gray-800">
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">DM %</div>
                  <div class="font-bold text-gray-900 dark:text-white text-sm mt-0.5">{{ ing.dryMatterPct }}%</div>
                </div>
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">CP %</div>
                  <div class="font-bold text-emerald-600 dark:text-emerald-400 text-sm mt-0.5">{{ ing.crudeProteinPct }}%</div>
                </div>
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">ME (MJ)</div>
                  <div class="font-bold text-gray-900 dark:text-white text-sm mt-0.5">{{ ing.metabolizableEnergyMjPerKg }}</div>
                </div>
              </div>

              <div class="mt-4 flex items-center justify-between">
                <span class="text-xs text-gray-400">Unit Cost</span>
                <span class="text-base font-extrabold text-emerald-600 dark:text-emerald-400">৳ {{ ing.unitCostBdt }} / {{ ing.unit }}</span>
              </div>
            </div>

            <!-- Footer Action -->
            <div class="p-3 bg-gray-50/80 dark:bg-gray-800/80 border-t border-gray-100 dark:border-gray-700/50 flex justify-end relative z-10">
              <button (click)="openEditDialog(ing)" class="px-3 py-1.5 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:bg-white dark:hover:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-700 transition-colors shadow-sm inline-flex items-center gap-1">
                <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">edit</mat-icon> Edit
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IngredientListComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);

  readonly isLoading = signal(true);
  readonly allIngredients = signal<FeedIngredient[]>([]);
  readonly filteredIngredients = signal<FeedIngredient[]>([]);

  searchTerm = '';
  selectedCategory: FeedCategory | null = null;

  readonly categoryOptions = [
    { value: FeedCategory.Forage, label: FeedCategoryNames[FeedCategory.Forage] },
    { value: FeedCategory.Concentrate, label: FeedCategoryNames[FeedCategory.Concentrate] },
    { value: FeedCategory.Mineral, label: FeedCategoryNames[FeedCategory.Mineral] },
    { value: FeedCategory.Additive, label: FeedCategoryNames[FeedCategory.Additive] },
    { value: FeedCategory.Silage, label: FeedCategoryNames[FeedCategory.Silage] },
    { value: FeedCategory.Byproduct, label: FeedCategoryNames[FeedCategory.Byproduct] },
  ];

  ngOnInit(): void {
    this.loadIngredients();
  }

  loadIngredients(): void {
    this.isLoading.set(true);
    this.feedingService.getIngredients(true).subscribe({
      next: (res) => {
        this.allIngredients.set(res);
        this.applyFilter();
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  applyFilter(): void {
    let list = this.allIngredients();
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      list = list.filter(i => i.name.toLowerCase().includes(term));
    }

    if (this.selectedCategory !== null) {
      list = list.filter(i => i.category === this.selectedCategory);
    }

    this.filteredIngredients.set(list);
  }

  openCreateIngredientDialog(): void {
    const dialogRef = this.dialog.open(CreateIngredientDialogComponent, { width: '600px' });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadIngredients();
    });
  }

  openEditDialog(ingredient: FeedIngredient): void {
    const dialogRef = this.dialog.open(CreateIngredientDialogComponent, {
      width: '600px',
      data: ingredient
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadIngredients();
    });
  }
}
