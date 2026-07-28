import { Component, forwardRef, inject, Input, OnInit, DestroyRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormControl, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { AnimalService } from '../../../features/livestock/services/animal.service';
import { ShedService } from '../../../features/farms/services/shed.service';
import { ShedList } from '../../../features/farms/models/shed.model';
import { AnimalListItemDto, AnimalSex, AnimalStatus } from '../../../features/livestock/models/animal.models';
import { WorkingContextService } from '../../../core/services/working-context.service';
import { debounceTime, distinctUntilChanged, switchMap, catchError, map, filter, startWith, tap } from 'rxjs/operators';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-animal-picker',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatButtonModule
  ],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => AnimalPickerComponent),
      multi: true
    }
  ],
  template: `
    <div class="flex flex-col gap-4">
      <div class="space-y-1.5">
        <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Shed (Optional)</label>
        <select [formControl]="shedControl"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          <option [ngValue]="null">-- All Sheds --</option>
          <option *ngFor="let shed of sheds$ | async" [value]="shed.id">
            {{ shed.shedName }}
          </option>
        </select>
      </div>

      <!-- Animal Autocomplete -->
      <div class="space-y-1.5 relative w-full">
        <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Search Animal (Tag, Breed)</label>
        
        <div class="relative flex items-center min-h-[42px] border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 focus-within:ring-2 focus-within:ring-primary-500 focus-within:border-primary-500 transition-shadow">
          <input type="text"
                 class="outline-none bg-transparent text-sm w-full h-full px-3 py-2 text-gray-900 dark:text-white placeholder-gray-400 rounded-lg"
                 placeholder="Type tag or breed..."
                 [formControl]="searchControl" 
                 [matAutocomplete]="auto">
                 
          <div class="absolute right-3 flex items-center">
            <mat-icon *ngIf="!isLoading" class="text-gray-400 !text-[20px] !w-[20px] !h-[20px]">search</mat-icon>
            <mat-spinner diameter="20" *ngIf="isLoading" class="text-primary-500"></mat-spinner>
          </div>
        </div>
        
        <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayFn" (optionSelected)="onAnimalSelected($event.option.value)" class="!rounded-lg !mt-1 shadow-lg border border-gray-100 dark:border-gray-700">
          <mat-option *ngFor="let animal of filteredAnimals$ | async" [value]="animal" class="!h-auto !py-2 border-b border-gray-50 dark:border-gray-800 last:border-0">
            <div class="flex flex-col">
              <span class="font-medium text-gray-900 dark:text-white text-sm">{{ animal.tagId }} — {{ animal.breedName }}</span>
              <span class="text-xs text-gray-500 flex gap-2 mt-0.5">
                <span>{{ getSexLabel(animal.sex) }}</span>
                <span *ngIf="animal.shedId"> • Shed ID: {{ animal.shedId | slice:0:8 }}</span>
              </span>
            </div>
          </mat-option>
        </mat-autocomplete>
      </div>
      
      <!-- Selected Animal Chip -->
      <div *ngIf="selectedAnimal" class="p-3 bg-primary-50 dark:bg-primary-900/20 border border-primary-100 dark:border-primary-800/50 rounded-lg flex items-center justify-between transition-colors">
        <div class="flex items-center gap-3">
          <div class="bg-primary-200 dark:bg-primary-800/60 text-primary-800 dark:text-primary-300 rounded-full h-10 w-10 flex items-center justify-center font-bold text-lg shadow-sm">
            🐄
          </div>
          <div class="flex flex-col">
            <span class="font-bold text-primary-900 dark:text-primary-100">{{ selectedAnimal.tagId }}</span>
            <span class="text-xs font-medium text-primary-700 dark:text-primary-400 mt-0.5">{{ selectedAnimal.breedName }} • {{ getSexLabel(selectedAnimal.sex) }}</span>
          </div>
        </div>
        <button type="button" (click)="clearSelection()" title="Clear selection"
                class="w-8 h-8 flex items-center justify-center rounded-full text-red-500 hover:bg-red-100 dark:hover:bg-red-900/30 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>
    </div>
  `
})
export class AnimalPickerComponent implements OnInit, ControlValueAccessor {
  private animalService = inject(AnimalService);
  private shedService = inject(ShedService);
  private contextService = inject(WorkingContextService);
  private destroyRef = inject(DestroyRef);

  @Input() requiredStatus?: AnimalStatus = AnimalStatus.Active;
  
  shedControl = new FormControl<string | null>({ value: null, disabled: true });
  searchControl = new FormControl<string | AnimalListItemDto>({ value: '', disabled: true });
  
  sheds$ = new BehaviorSubject<ShedList[]>([]);
  filteredAnimals$: Observable<AnimalListItemDto[]>;
  
  selectedAnimal: AnimalListItemDto | null = null;
  isLoading = false;

  // ControlValueAccessor methods
  onChange: any = () => {};
  onTouched: any = () => {};
  isDisabled = false;
  
  private currentFarmId: string | null = null;

  constructor() {
    this.filteredAnimals$ = this.searchControl.valueChanges.pipe(
      startWith(''),
      filter(value => typeof value === 'string'),
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => this.isLoading = true),
      switchMap(value => {
        if (!this.currentFarmId) {
          this.isLoading = false;
          return of([]);
        }
        
        return this.animalService.getList({
          farmId: this.currentFarmId,
          shedId: this.shedControl.value || undefined,
          search: value as string,
          status: this.requiredStatus,
          pageSize: 20
        }).pipe(
          map(res => res.items),
          catchError(() => of([])),
          tap(() => this.isLoading = false)
        );
      })
    );
  }

  ngOnInit() {
    this.contextService.currentFarm$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(farm => {
      this.currentFarmId = farm?.id || null;
      this.clearSelection();
      this.searchControl.setValue('');
      this.shedControl.setValue(null);
      
      if (this.currentFarmId) {
        this.shedControl.enable({ emitEvent: false });
        this.searchControl.enable({ emitEvent: false });
        this.shedService.getShedsByFarm(this.currentFarmId).subscribe(sheds => {
          this.sheds$.next(sheds);
        });
      } else {
        this.shedControl.disable({ emitEvent: false });
        this.searchControl.disable({ emitEvent: false });
        this.sheds$.next([]);
      }
    });

    this.shedControl.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.clearSelection();
      this.searchControl.setValue(''); // Trigger new search
    });
  }

  displayFn(animal: AnimalListItemDto): string {
    return animal && animal.tagId ? animal.tagId : '';
  }

  onAnimalSelected(animal: AnimalListItemDto) {
    this.selectedAnimal = animal;
    this.onChange(animal.id);
  }

  clearSelection() {
    this.selectedAnimal = null;
    this.searchControl.setValue('');
    this.onChange(null);
  }

  getSexLabel(sex: AnimalSex): string {
    return sex === AnimalSex.Male ? 'Male' : sex === AnimalSex.Female ? 'Female' : 'Unknown';
  }

  writeValue(obj: any): void {
    if (!obj) {
      this.selectedAnimal = null;
      this.searchControl.setValue('', { emitEvent: false });
      return;
    }
    
    if (typeof obj === 'string' && (!this.selectedAnimal || this.selectedAnimal.id !== obj)) {
      this.isLoading = true;
      this.animalService.getById(obj).subscribe({
        next: (animal) => {
          this.selectedAnimal = animal as unknown as AnimalListItemDto;
          this.searchControl.setValue(this.selectedAnimal, { emitEvent: false });
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
        }
      });
    }
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.isDisabled = isDisabled;
    if (isDisabled) {
      this.shedControl.disable({ emitEvent: false });
      this.searchControl.disable({ emitEvent: false });
    } else {
      if (this.currentFarmId) {
        this.shedControl.enable({ emitEvent: false });
        this.searchControl.enable({ emitEvent: false });
      }
    }
  }
}
