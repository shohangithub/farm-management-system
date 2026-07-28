import { Component, Input, OnInit, forwardRef, inject, signal, computed, ChangeDetectionStrategy, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
import { LocationService } from '../../services/location.service';
import { Country, Division, District, Upazila, Union, Village } from '../../models/location.model';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-location-selector',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './location-selector.component.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => LocationSelectorComponent),
      multi: true
    }
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LocationSelectorComponent implements OnInit, ControlValueAccessor {
  @Input() label: string = 'Location';
  @Input() required: boolean = false;
  // This specifies what the final value of this component should be (e.g. 'district', 'village')
  @Input() selectionLevel: 'country' | 'division' | 'district' | 'upazila' | 'union' | 'village' = 'village';

  private locationService = inject(LocationService);
  private destroyRef = inject(DestroyRef);

  countries = signal<Country[]>([]);
  divisions = signal<Division[]>([]);
  districts = signal<District[]>([]);
  upazilas = signal<Upazila[]>([]);
  unions = signal<Union[]>([]);
  villages = signal<Village[]>([]);

  selectedCountryId = signal<string>('');
  selectedDivisionId = signal<string>('');
  selectedDistrictId = signal<string>('');
  selectedUpazilaId = signal<string>('');
  selectedUnionId = signal<string>('');
  selectedVillageId = signal<string>('');

  isDisabled = signal<boolean>(false);

  onChange: any = () => {};
  onTouched: any = () => {};

  ngOnInit(): void {
    this.locationService.getCountries().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => this.countries.set(data));
  }

  // Value is simply set but reconstructing the tree from bottom up requires an API that returns parents.
  // For simplicity in this reusable component, if value is set externally, we only emit changes.
  writeValue(val: string): void {
    // Advanced: If val is provided, we would need to recursively fetch parents to populate dropdowns.
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.isDisabled.set(isDisabled);
  }

  private emitValue(val: string): void {
    this.onChange(val);
    this.onTouched();
  }

  onCountryChange(val: string): void {
    this.selectedCountryId.set(val);
    
    this.divisions.set([]);
    this.districts.set([]);
    this.upazilas.set([]);
    this.unions.set([]);
    this.villages.set([]);
    
    this.selectedDivisionId.set('');
    this.selectedDistrictId.set('');
    this.selectedUpazilaId.set('');
    this.selectedUnionId.set('');
    this.selectedVillageId.set('');

    if (val) {
      this.locationService.getDivisions(val).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => this.divisions.set(data));
      if (this.selectionLevel === 'country') this.emitValue(val);
    }
  }

  onDivisionChange(val: string): void {
    this.selectedDivisionId.set(val);
    
    this.districts.set([]);
    this.upazilas.set([]);
    this.unions.set([]);
    this.villages.set([]);
    
    this.selectedDistrictId.set('');
    this.selectedUpazilaId.set('');
    this.selectedUnionId.set('');
    this.selectedVillageId.set('');

    if (val) {
      this.locationService.getDistricts(val).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => this.districts.set(data));
      if (this.selectionLevel === 'division') this.emitValue(val);
    }
  }

  onDistrictChange(val: string): void {
    this.selectedDistrictId.set(val);
    
    this.upazilas.set([]);
    this.unions.set([]);
    this.villages.set([]);
    
    this.selectedUpazilaId.set('');
    this.selectedUnionId.set('');
    this.selectedVillageId.set('');

    if (val) {
      this.locationService.getUpazilas(val).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => this.upazilas.set(data));
      if (this.selectionLevel === 'district') this.emitValue(val);
    }
  }

  onUpazilaChange(val: string): void {
    this.selectedUpazilaId.set(val);
    
    this.unions.set([]);
    this.villages.set([]);
    
    this.selectedUnionId.set('');
    this.selectedVillageId.set('');

    if (val) {
      this.locationService.getUnions(val).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => this.unions.set(data));
      if (this.selectionLevel === 'upazila') this.emitValue(val);
    }
  }

  onUnionChange(val: string): void {
    this.selectedUnionId.set(val);
    
    this.villages.set([]);
    this.selectedVillageId.set('');

    if (val) {
      this.locationService.getVillages(val).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => this.villages.set(data));
      if (this.selectionLevel === 'union') this.emitValue(val);
    }
  }

  onVillageChange(val: string): void {
    this.selectedVillageId.set(val);
    if (val && this.selectionLevel === 'village') {
      this.emitValue(val);
    }
  }
}
