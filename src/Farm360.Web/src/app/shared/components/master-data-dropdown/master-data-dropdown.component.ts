import { Component, Input, OnInit, forwardRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
import { MasterDataService } from '../../services/master-data.service';
import { MasterDataType, MasterDataEntry } from '../../models/master-data.model';

@Component({
  selector: 'app-master-data-dropdown',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './master-data-dropdown.component.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MasterDataDropdownComponent),
      multi: true
    }
  ]
})
export class MasterDataDropdownComponent implements OnInit, ControlValueAccessor {
  @Input() type!: MasterDataType;
  @Input() label: string = '';
  @Input() placeholder: string = 'Select an option';
  @Input() required: boolean = false;

  private masterDataService = inject(MasterDataService);
  
  options: MasterDataEntry[] = [];
  value: string = '';
  isDisabled: boolean = false;
  isLoading: boolean = true;

  onChange: any = () => {};
  onTouched: any = () => {};

  ngOnInit(): void {
    if (!this.type) {
      console.error('MasterDataType is required for master-data-dropdown');
      return;
    }

    this.masterDataService.getByType(this.type).subscribe({
      next: (data) => {
        this.options = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error(`Failed to load master data for type ${this.type}`, err);
        this.isLoading = false;
      }
    });
  }

  writeValue(val: string): void {
    this.value = val;
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

  onSelectChange(event: any): void {
    this.value = event.target.value;
    this.onChange(this.value);
    this.onTouched();
  }
}
