import { Component, forwardRef, inject, Input, OnInit, OnDestroy } from '@angular/core';
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
import { BehaviorSubject, Observable, of, Subject, takeUntil } from 'rxjs';

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

      <!-- Selected Animals Chips -->
      <mat-form-field appearance="outline" class="w-full">
        <mat-label>Search and Select Animals</mat-label>
        
        <mat-chip-grid #chipGrid aria-label="Selected animals">
          <mat-chip-row *ngFor="let animal of selectedAnimals" (removed)="removeAnimal(animal)">
            {{ animal.tagId }}
            <button matChipRemove [attr.aria-label]="'remove ' + animal.tagId">
              <mat-icon>cancel</mat-icon>
            </button>
          </mat-chip-row>
          
          <input 
            placeholder="Type tag or breed..." 
            [matChipInputFor]="chipGrid" 
            [formControl]="searchControl" 
            [matAutocomplete]="auto">
        </mat-chip-grid>
        
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
    </div>
  `
})
export class AnimalMultiPickerComponent implements OnInit, OnDestroy, ControlValueAccessor {
  private animalService = inject(AnimalService);
  private shedService = inject(ShedService);
  private contextService = inject(WorkingContextService);

  @Input() requiredStatus?: AnimalStatus = AnimalStatus.Active;
  
  shedControl = new FormControl<string | null>({ value: null, disabled: true });
  searchControl = new FormControl<string | AnimalListItemDto>({ value: '', disabled: true });
  
  sheds$ = new BehaviorSubject<ShedList[]>([]);
  filteredAnimals$: Observable<AnimalListItemDto[]>;
  
  selectedAnimals: AnimalListItemDto[] = [];
  isLoading = false;
  private destroy$ = new Subject<void>();
  
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
      takeUntil(this.destroy$)
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
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.searchControl.setValue(''); // Trigger new search
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
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
