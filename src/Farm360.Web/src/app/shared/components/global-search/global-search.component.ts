import { Component, HostListener, ViewChild, ElementRef, inject, signal, computed, ChangeDetectionStrategy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { AnimalService } from '../../../features/livestock/services/animal.service';
import { InventoryService } from '../../../features/inventory/services/inventory.service';
import { WorkingContextService } from '../../../core/services/working-context.service';
import { debounceTime, distinctUntilChanged, switchMap, catchError, map, filter, tap } from 'rxjs/operators';
import { of, forkJoin } from 'rxjs';
import { AnimalListItemDto } from '../../../features/livestock/models/animal.models';
import { InventoryItem } from '../../../features/inventory/models/inventory.models';

interface SearchResultGroup {
  name: string;
  items: { id: string, title: string, subtitle: string, icon: string, route: string }[];
}

@Component({
  selector: 'app-global-search',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MatIconModule],
  template: `
    <div class="relative w-full group" (clickOutside)="close()">
      <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
        <mat-icon class="text-gray-400 dark:text-gray-500 text-[20px] h-5 w-5 group-focus-within:text-primary-500 transition-colors">search</mat-icon>
      </div>
      <input #searchInput type="text" [formControl]="searchControl" placeholder="Search everywhere..." 
             (focus)="isOpen.set(true)"
             class="block w-full pl-10 pr-12 py-1.5 border border-gray-200 dark:border-gray-700/80 rounded-md text-[13px] leading-5 bg-gray-50/50 dark:bg-gray-800/50 placeholder-gray-400 focus:outline-none focus:bg-white dark:focus:bg-gray-800 focus:ring-2 focus:ring-primary-500/20 focus:border-primary-500 text-gray-900 dark:text-white transition-all duration-200">
      
      <div class="absolute inset-y-0 right-0 pr-2 flex items-center">
        <span *ngIf="isLoading()" class="mr-2">
          <mat-icon class="animate-spin text-primary-500 !text-[16px] !w-[16px] !h-[16px]">autorenew</mat-icon>
        </span>
        <span *ngIf="!isLoading()" class="text-gray-400 dark:text-gray-500 text-[10px] font-semibold border border-gray-200 dark:border-gray-700 rounded px-1.5 py-0.5 bg-white dark:bg-gray-800 pointer-events-none">⌘K</span>
      </div>

      <!-- Dropdown Results -->
      <div *ngIf="isOpen() && (hasResults() || isLoading() || searchControl.value)" 
           class="absolute top-full left-0 right-0 mt-2 bg-white dark:bg-gray-800 rounded-lg shadow-xl border border-gray-100 dark:border-gray-700 max-h-96 overflow-y-auto z-50">
        
        <div *ngIf="isLoading()" class="p-4 text-center text-sm text-gray-500 dark:text-gray-400">
          Searching...
        </div>

        <div *ngIf="!isLoading() && !hasResults() && searchControl.value" class="p-4 text-center text-sm text-gray-500 dark:text-gray-400">
          No results found for "{{ searchControl.value }}"
        </div>

        <ng-container *ngIf="!isLoading() && hasResults()">
          <div *ngFor="let group of results()" class="py-2">
            <h3 class="px-4 py-1 text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wider">{{ group.name }}</h3>
            <ul>
              <li *ngFor="let item of group.items">
                <a (click)="navigate(item.route)" class="flex items-center gap-3 px-4 py-2 hover:bg-gray-50 dark:hover:bg-gray-700/50 cursor-pointer transition-colors">
                  <div class="flex-shrink-0 w-8 h-8 bg-gray-100 dark:bg-gray-900 rounded-lg flex items-center justify-center text-gray-500 dark:text-gray-400">
                    <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">{{ item.icon }}</mat-icon>
                  </div>
                  <div class="min-w-0 flex-1">
                    <p class="text-sm font-medium text-gray-900 dark:text-white truncate">{{ item.title }}</p>
                    <p class="text-xs text-gray-500 dark:text-gray-400 truncate">{{ item.subtitle }}</p>
                  </div>
                </a>
              </li>
            </ul>
          </div>
        </ng-container>

      </div>
    </div>
  `
})
export class GlobalSearchComponent implements OnInit {
  @ViewChild('searchInput') searchInput!: ElementRef<HTMLInputElement>;
  
  private readonly router = inject(Router);
  private readonly animalSvc = inject(AnimalService);
  private readonly inventorySvc = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);

  readonly searchControl = new FormControl('');
  readonly isOpen = signal(false);
  readonly isLoading = signal(false);
  readonly results = signal<SearchResultGroup[]>([]);
  readonly hasResults = computed(() => this.results().length > 0);

  @HostListener('window:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent) {
    if ((event.metaKey || event.ctrlKey) && event.key === 'k') {
      event.preventDefault();
      if (this.searchInput && this.searchInput.nativeElement) {
        this.searchInput.nativeElement.focus();
      }
    }
    if (event.key === 'Escape' && this.isOpen()) {
      this.close();
    }
  }

  @HostListener('document:click', ['$event'])
  clickout(event: Event) {
    if (this.searchInput && this.searchInput.nativeElement && this.searchInput.nativeElement.parentElement) {
      if (!this.searchInput.nativeElement.parentElement.contains(event.target as Node)) {
        this.close();
      }
    }
  }

  ngOnInit() {
    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => {
        this.isLoading.set(true);
        if (!this.searchControl.value) {
          this.results.set([]);
          this.isLoading.set(false);
        }
      }),
      filter(val => !!val),
      switchMap(term => {
        let currentFarmId = '';
        this.contextService.currentFarm$.subscribe((f: any) => {
          if (f && f.id) currentFarmId = f.id as string;
        }).unsubscribe();

        if (!currentFarmId) return of([]);

        const animals$ = this.animalSvc.getList({ search: term!, farmId: currentFarmId, pageSize: 5 }).pipe(
          map(res => res.items),
          catchError(() => of([]))
        );

        const inventory$ = this.inventorySvc.getItems({ search: term!, farmId: currentFarmId, pageSize: 5 }).pipe(
          map(res => res.items),
          catchError(() => of([]))
        );

        return forkJoin({ animals: animals$, inventory: inventory$ });
      })
    ).subscribe((data: any) => {
      if (!data) return;
      
      const groups: SearchResultGroup[] = [];
      
      if (data.animals && data.animals.length > 0) {
        groups.push({
          name: 'Animals',
          items: data.animals.map((a: AnimalListItemDto) => ({
            id: a.id,
            title: `Tag: ${a.tagId}`,
            subtitle: `${a.species} - ${a.breedName}`,
            icon: 'pets',
            route: `/livestock/animals/${a.id}`
          }))
        });
      }

      if (data.inventory && data.inventory.length > 0) {
        groups.push({
          name: 'Inventory',
          items: data.inventory.map((i: InventoryItem) => ({
            id: i.id,
            title: i.name,
            subtitle: `Category: ${i.category}`,
            icon: 'inventory_2',
            route: `/inventory/items/${i.id}`
          }))
        });
      }

      this.results.set(groups);
      this.isLoading.set(false);
      this.isOpen.set(true);
    });
  }

  navigate(route: string) {
    this.router.navigateByUrl(route);
    this.close();
  }

  close() {
    this.isOpen.set(false);
  }
}
