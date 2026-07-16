import { Component, Input, OnInit, forwardRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
import { LocationService } from '../../services/location.service';
import { Country, Division, District, Upazila, Union, Village } from '../../models/location.model';

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
  ]
})
export class LocationSelectorComponent implements OnInit, ControlValueAccessor {
  @Input() label: string = 'Location';
  @Input() required: boolean = false;
  // This specifies what the final value of this component should be (e.g. 'district', 'village')
  @Input() selectionLevel: 'country' | 'division' | 'district' | 'upazila' | 'union' | 'village' = 'village';

  private locationService = inject(LocationService);

  countries: Country[] = [];
  divisions: Division[] = [];
  districts: District[] = [];
  upazilas: Upazila[] = [];
  unions: Union[] = [];
  villages: Village[] = [];

  selectedCountryId: string = '';
  selectedDivisionId: string = '';
  selectedDistrictId: string = '';
  selectedUpazilaId: string = '';
  selectedUnionId: string = '';
  selectedVillageId: string = '';

  isDisabled: boolean = false;

  onChange: any = () => {};
  onTouched: any = () => {};

  ngOnInit(): void {
    this.locationService.getCountries().subscribe(data => this.countries = data);
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
    this.isDisabled = isDisabled;
  }

  private emitValue(val: string): void {
    this.onChange(val);
    this.onTouched();
  }

  onCountryChange(): void {
    this.divisions = [];
    this.districts = [];
    this.upazilas = [];
    this.unions = [];
    this.villages = [];
    
    this.selectedDivisionId = '';
    this.selectedDistrictId = '';
    this.selectedUpazilaId = '';
    this.selectedUnionId = '';
    this.selectedVillageId = '';

    if (this.selectedCountryId) {
      this.locationService.getDivisions(this.selectedCountryId).subscribe(data => this.divisions = data);
      if (this.selectionLevel === 'country') this.emitValue(this.selectedCountryId);
    }
  }

  onDivisionChange(): void {
    this.districts = [];
    this.upazilas = [];
    this.unions = [];
    this.villages = [];
    
    this.selectedDistrictId = '';
    this.selectedUpazilaId = '';
    this.selectedUnionId = '';
    this.selectedVillageId = '';

    if (this.selectedDivisionId) {
      this.locationService.getDistricts(this.selectedDivisionId).subscribe(data => this.districts = data);
      if (this.selectionLevel === 'division') this.emitValue(this.selectedDivisionId);
    }
  }

  onDistrictChange(): void {
    this.upazilas = [];
    this.unions = [];
    this.villages = [];
    
    this.selectedUpazilaId = '';
    this.selectedUnionId = '';
    this.selectedVillageId = '';

    if (this.selectedDistrictId) {
      this.locationService.getUpazilas(this.selectedDistrictId).subscribe(data => this.upazilas = data);
      if (this.selectionLevel === 'district') this.emitValue(this.selectedDistrictId);
    }
  }

  onUpazilaChange(): void {
    this.unions = [];
    this.villages = [];
    
    this.selectedUnionId = '';
    this.selectedVillageId = '';

    if (this.selectedUpazilaId) {
      this.locationService.getUnions(this.selectedUpazilaId).subscribe(data => this.unions = data);
      if (this.selectionLevel === 'upazila') this.emitValue(this.selectedUpazilaId);
    }
  }

  onUnionChange(): void {
    this.villages = [];
    this.selectedVillageId = '';

    if (this.selectedUnionId) {
      this.locationService.getVillages(this.selectedUnionId).subscribe(data => this.villages = data);
      if (this.selectionLevel === 'union') this.emitValue(this.selectedUnionId);
    }
  }

  onVillageChange(): void {
    if (this.selectedVillageId && this.selectionLevel === 'village') {
      this.emitValue(this.selectedVillageId);
    }
  }
}
