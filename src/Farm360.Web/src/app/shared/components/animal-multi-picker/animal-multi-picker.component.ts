import { Component, forwardRef, inject, Input, OnInit, DestroyRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormControl, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { AnimalService } from '../../../features/livestock/services/animal.service';
import { ShedService } from '../../../features/farms/services/shed.service';
import { ShedList } from '../../../features/farms/models/shed.model';
import { AnimalListItemDto, AnimalSex, AnimalStatus } from '../../../features/livestock/models/animal.models';
import { WorkingContextService } from '../../../core/services/working-context.service';
import { debounceTime, distinctUntilChanged, switchMap, catchError, map, filter, startWith, tap } from 'rxjs/operators';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-animal-multi-picker',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatFormFieldModule, 
    MatInputModule, 
    MatSelectModule, 
    MatAutocompleteModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule
  ],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => AnimalMultiPickerComponent),
      multi: true
    }
  ],
  template: `
    <div class="flex flex-col gap-4">
      <div class="space-y-1.5">
        <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">Shed (Optional)</label>
        <select [formControl]="shedControl"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          <option [ngValue]="null">-- All Sheds --</option>
          <option *ngFor="let shed of sheds$ | async" [value]="shed.id">
            {{ shed.shedName }}
          </option>
        </select>
      </div>

      <!-- Selected Animals Chips -->
      <div class="space-y-1.5 relative w-full">
        <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">Search and Select Animals</label>
        
        <div class="flex items-center min-h-[42px] px-3 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 focus-within:ring-2 focus-within:ring-primary-500 focus-within:border-primary-500 transition-shadow flex-wrap gap-2">
          
          <mat-chip-grid #chipGrid aria-label="Selected animals" class="!flex !flex-row !flex-wrap !items-center !gap-1.5">
            <mat-chip-row *ngFor="let animal of selectedAnimals" (removed)="removeAnimal(animal)"
                          class="!bg-primary-50 dark:!bg-primary-900/30 !text-primary-700 dark:!text-primary-300 !min-h-[28px] border border-primary-200 dark:border-primary-800/50">
              <span class="text-xs font-medium">{{ animal.tagId }}</span>
              <button matChipRemove [attr.aria-label]="'remove ' + animal.tagId" class="opacity-70 hover:opacity-100">
                <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">cancel</mat-icon>
              </button>
            </mat-chip-row>
            
            <input 
              class="outline-none bg-transparent text-sm flex-1 min-w-[150px] text-gray-900 dark:text-white placeholder-gray-400 py-1"
              placeholder="Type tag or breed..." 
              [matChipInputFor]="chipGrid" 
              [formControl]="searchControl" 
              [matAutocomplete]="auto">
          </mat-chip-grid>
          
          <mat-spinner diameter="20" *ngIf="isLoading" class="ml-auto shrink-0"></mat-spinner>
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
    </div>
  `
})
export class AnimalMultiPickerComponent implements OnInit, ControlValueAccessor {
  private animalService = inject(AnimalService);
  private shedService = inject(ShedService);
  private contextService = inject(WorkingContextService);
  private destroyRef = inject(DestroyRef);

  @Input() requiredStatus?: AnimalStatus = AnimalStatus.Active;
  
  shedControl = new FormControl<string | null>({ value: null, disabled: true });
  searchControl = new FormControl<string | AnimalListItemDto>({ value: '', disabled: true });
  
  sheds$ = new BehaviorSubject<ShedList[]>([]);
  filteredAnimals$: Observable<AnimalListItemDto[]>;
  
  selectedAnimals: AnimalListItemDto[] = [];
  isLoading = false;
  
  private currentFarmId: string | null = null;

  // ControlValueAccessor methods
  onChange: any = () => {};
  onTouched: any = () => {};
  isDisabled = false;

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
          map(res => {
            const selectedIds = new Set(this.selectedAnimals.map(a => a.id));
            return res.items.filter(item => !selectedIds.has(item.id));
          }),
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
      this.searchControl.setValue(''); // Trigger new search
    });
  }

  displayFn(animal: AnimalListItemDto): string {
    return '';
  }

  onAnimalSelected(animal: AnimalListItemDto) {
    if (!this.selectedAnimals.find(a => a.id === animal.id)) {
      this.selectedAnimals.push(animal);
      this.emitValue();
    }
    this.searchControl.setValue('');
  }

  removeAnimal(animal: AnimalListItemDto) {
    const index = this.selectedAnimals.indexOf(animal);
    if (index >= 0) {
      this.selectedAnimals.splice(index, 1);
      this.emitValue();
    }
  }

  private emitValue() {
    this.onChange(this.selectedAnimals.map(a => a.id));
  }

  getSexLabel(sex: AnimalSex): string {
    return sex === AnimalSex.Male ? 'Male' : sex === AnimalSex.Female ? 'Female' : 'Unknown';
  }

  writeValue(obj: any): void {
    if (!obj || !Array.isArray(obj)) {
      this.selectedAnimals = [];
      this.searchControl.setValue('', { emitEvent: false });
      return;
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
