import { Component, Input, OnInit, forwardRef, inject, signal, computed, ChangeDetectionStrategy, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
import { MasterDataService } from '../../services/master-data.service';
import { MasterDataType, MasterDataEntry } from '../../models/master-data.model';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

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
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MasterDataDropdownComponent implements OnInit, ControlValueAccessor {
  @Input() type!: MasterDataType;
  @Input() label: string = '';
  @Input() placeholder: string = 'Select an option';
  @Input() required: boolean = false;

  private masterDataService = inject(MasterDataService);
  private destroyRef = inject(DestroyRef);
  
  options = signal<MasterDataEntry[]>([]);
  value = signal<string>('');
  isDisabled = signal<boolean>(false);
  isLoading = signal<boolean>(true);

  onChange: any = () => {};
  onTouched: any = () => {};

  ngOnInit(): void {
    if (!this.type) {
      console.error('MasterDataType is required for master-data-dropdown');
      this.isLoading.set(false);
      return;
    }

    this.masterDataService.getByType(this.type).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.options.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error(`Failed to load master data for type ${this.type}`, err);
        this.isLoading.set(false);
      }
    });
  }

  writeValue(val: string): void {
    this.value.set(val || '');
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

  onSelectChange(event: any): void {
    this.value.set(event.target.value);
    this.onChange(this.value());
    this.onTouched();
  }
}
