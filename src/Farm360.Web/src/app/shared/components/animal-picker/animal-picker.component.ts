import { Component, forwardRef, inject, Input, OnInit, OnDestroy } from '@angular/core';
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
import { BehaviorSubject, Observable, of, Subject, takeUntil } from 'rxjs';

@Component({
  selector: 'app-animal-picker',
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
      <div class="grid grid-cols-2 gap-4">
        <!-- Shed Selection -->
        <mat-form-field appearance="outline">
          <mat-label>Shed (Optional)</mat-label>
          <mat-select [formControl]="shedControl">
            <mat-option [value]="null">-- All Sheds --</mat-option>
            <mat-option *ngFor="let shed of sheds$ | async" [value]="shed.id">
              {{ shed.shedName }}
            </mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      <!-- Animal Autocomplete -->
      <mat-form-field appearance="outline" class="w-full">
        <mat-label>Search Animal (Tag, Breed)</mat-label>
        <input type="text" matInput [formControl]="searchControl" [matAutocomplete]="auto">
        <mat-icon matSuffix *ngIf="!isLoading">search</mat-icon>
        <mat-spinner matSuffix diameter="20" *ngIf="isLoading"></mat-spinner>
        
        <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayFn" (optionSelected)="onAnimalSelected($event.option.value)">
          <mat-option *ngFor="let animal of filteredAnimals$ | async" [value]="animal">
            <div class="flex flex-col py-1">
              <span class="font-medium text-gray-900">{{ animal.tagId }} — {{ animal.breedName }}</span>
              <span class="text-xs text-gray-500 flex gap-2">
                <span>{{ getSexLabel(animal.sex) }}</span>
                <span *ngIf="animal.shedId"> • Shed ID: {{ animal.shedId | slice:0:8 }}</span>
              </span>
            </div>
          </mat-option>
        </mat-autocomplete>
      </mat-form-field>
      
      <!-- Selected Animal Chip -->
      <div *ngIf="selectedAnimal" class="mt-1 mb-2 p-3 bg-blue-50 border border-blue-100 rounded-md flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="bg-blue-200 text-blue-800 rounded-full h-8 w-8 flex items-center justify-center font-bold">
            🐄
          </div>
          <div class="flex flex-col">
            <span class="font-medium text-blue-900">{{ selectedAnimal.tagId }}</span>
            <span class="text-xs text-blue-700">{{ selectedAnimal.breedName }} • {{ getSexLabel(selectedAnimal.sex) }}</span>
          </div>
        </div>
        <button mat-icon-button color="warn" (click)="clearSelection()" type="button" title="Clear selection">
          <mat-icon>close</mat-icon>
        </button>
      </div>
    </div>
  `
})
export class AnimalPickerComponent implements OnInit, OnDestroy, ControlValueAccessor {
  private animalService = inject(AnimalService);
  private shedService = inject(ShedService);
  private contextService = inject(WorkingContextService);

  @Input() requiredStatus?: AnimalStatus = AnimalStatus.Active;
  
  shedControl = new FormControl<string | null>({ value: null, disabled: true });
  searchControl = new FormControl<string | AnimalListItemDto>({ value: '', disabled: true });
  
  sheds$ = new BehaviorSubject<ShedList[]>([]);
  filteredAnimals$: Observable<AnimalListItemDto[]>;
  
  selectedAnimal: AnimalListItemDto | null = null;
  isLoading = false;
  private destroy$ = new Subject<void>();

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
      takeUntil(this.destroy$)
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
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.clearSelection();
      this.searchControl.setValue(''); // Trigger new search
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
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
