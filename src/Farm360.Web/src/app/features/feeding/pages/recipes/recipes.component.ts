import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { FeedingSubnavComponent } from '../../components/feeding-subnav/feeding-subnav.component';
import { FormulaListComponent } from '../formula-list/formula-list.component';
import { IngredientListComponent } from '../ingredient-list/ingredient-list.component';

type RecipeTab = 'recipes' | 'ingredients';

/** Recipes (feed formulas) and the ingredients they are mixed from, on one page. */
@Component({
  selector: 'app-recipes',
  standalone: true,
  imports: [CommonModule, MatIconModule, FeedingSubnavComponent, FormulaListComponent, IngredientListComponent],
  template: `
    <div class="px-6 pt-4 mx-auto max-w-7xl">
      <app-feeding-subnav></app-feeding-subnav>
      <div class="flex items-center gap-2 text-xs font-semibold">
        <button (click)="show('recipes')" class="px-3.5 py-2 rounded-xl inline-flex items-center gap-1.5"
          [ngClass]="tab() === 'recipes' ? 'bg-emerald-600 text-white' : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300'">
          <mat-icon class="!text-[16px] !w-4 !h-4">blender</mat-icon> Recipes · রেসিপি
        </button>
        <button (click)="show('ingredients')" class="px-3.5 py-2 rounded-xl inline-flex items-center gap-1.5"
          [ngClass]="tab() === 'ingredients' ? 'bg-emerald-600 text-white' : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300'">
          <mat-icon class="!text-[16px] !w-4 !h-4">grain</mat-icon> Ingredients · উপকরণ
        </button>
        <span class="text-gray-500 font-normal ml-2">
          {{ tab() === 'recipes' ? 'A recipe is a mix of ingredients by %, priced per kg.' : 'Ingredients link to stock items, so feeding deducts stock.' }}
        </span>
      </div>
    </div>
    <app-formula-list *ngIf="tab() === 'recipes'"></app-formula-list>
    <app-ingredient-list *ngIf="tab() === 'ingredients'"></app-ingredient-list>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipesComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly tab = signal<RecipeTab>(this.route.snapshot.queryParamMap.get('tab') === 'ingredients' ? 'ingredients' : 'recipes');

  show(tab: RecipeTab): void {
    this.tab.set(tab);
    void this.router.navigate([], { queryParams: { tab }, replaceUrl: true });
  }
}
