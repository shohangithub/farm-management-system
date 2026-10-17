import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { InventoryService } from '../../services/inventory.service';
import { ItemUnits, UnitConversion } from '../../models/inventory.models';

export interface UnitSelection {
  unitId: string | null;
  name: string;
  /** Base units in one of the chosen unit. */
  factor: number;
  baseUnit: string;
}

/**
 * Unit dropdown for a stock item: its base unit and the packs it is bought or used in
 * ("Bag (50 kg)", "Bottle (100 ml)"). Shows what the quantity comes to in the base unit.
 * Stock is always kept in the base unit; the server converts.
 */
@Component({
  selector: 'app-unit-picker',
  standalone: true,
  imports: [CommonModule, MatFormFieldModule, MatSelectModule],
  template: `
    <!-- Inside Material (outline) forms, match the fields around it -->
    <mat-form-field *ngIf="appearance() === 'material'; else plain" appearance="outline" class="w-full">
      <mat-label>{{ label() || 'Unit' }}</mat-label>
      <mat-select [value]="selectedId()" (selectionChange)="choose($event.value)" [disabled]="!units()?.units?.length">
        <mat-option *ngIf="!units()?.units?.length" [value]="null">{{ units()?.baseUnit || '—' }}</mat-option>
        <mat-option *ngFor="let u of units()?.units" [value]="u.id">
          {{ u.name }}{{ u.isBase ? '' : ' = ' + (u.factor | number:'1.0-4') + ' ' + units()!.baseUnit }}
        </mat-option>
      </mat-select>
      <mat-hint *ngIf="hint() as h">{{ h }}</mat-hint>
    </mat-form-field>

    <ng-template #plain>
    <div class="flex flex-col gap-1">
      <label *ngIf="label()" class="text-xs font-semibold text-gray-600 dark:text-gray-300">{{ label() }}</label>
      <select [disabled]="!units()?.units?.length"
              (change)="choose($any($event.target).value)"
              class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-sm text-gray-900 dark:text-white outline-none focus:border-emerald-500">
        <option *ngIf="!units()?.units?.length" value="">{{ units()?.baseUnit || '—' }}</option>
        <option *ngFor="let u of units()?.units" [value]="u.id" [selected]="u.id === selectedId()">
          {{ u.name }}{{ u.isBase ? '' : ' = ' + (u.factor | number:'1.0-4') + ' ' + units()!.baseUnit }}
        </option>
      </select>
      <span *ngIf="hint() as h" class="text-[11px] text-gray-500 dark:text-gray-400">{{ h }}</span>
    </div>
    </ng-template>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnitPickerComponent {
  private readonly inventory = inject(InventoryService);

  readonly itemId = input<string | null | undefined>(null);
  readonly quantity = input<number | null | undefined>(null);
  readonly label = input<string>('');
  /** 'material' renders an outlined mat-select to sit among Material form fields. */
  readonly appearance = input<'plain' | 'material'>('plain');
  /** Start on the item's default (purchase) unit instead of its base unit. */
  readonly useDefault = input(true);

  readonly selectionChange = output<UnitSelection>();

  readonly units = signal<ItemUnits | null>(null);
  readonly selectedId = signal<string | null>(null);

  readonly selected = computed<UnitConversion | undefined>(() =>
    this.units()?.units.find(u => u.id === this.selectedId()));

  readonly hint = computed(() => {
    const u = this.selected();
    const q = Number(this.quantity()) || 0;
    const base = this.units()?.baseUnit;
    if (!u || u.isBase || !q || !base) return '';
    return `= ${(q * u.factor).toLocaleString('en-IN', { maximumFractionDigits: 4 })} ${base}`;
  });

  constructor() {
    effect(() => {
      const id = this.itemId();
      untracked(() => this.load(id));
    });
  }

  private load(itemId: string | null | undefined): void {
    this.units.set(null);
    this.selectedId.set(null);
    if (!itemId) return;
    this.inventory.getItemUnits(itemId).subscribe(res => {
      this.units.set(res);
      const base = res.units.find(u => u.isBase) ?? res.units[0];
      const start = (this.useDefault() && res.units.find(u => u.id === res.defaultUnitId)) || base;
      this.choose(start?.id ?? '');
    });
  }

  choose(id: string): void {
    const res = this.units();
    const u = res?.units.find(x => x.id === id);
    this.selectedId.set(u?.id ?? null);
    this.selectionChange.emit({
      // The base unit itself needs no conversion on the server.
      unitId: u && !u.isBase ? u.id : null,
      name: u?.name ?? res?.baseUnit ?? '',
      factor: u?.factor ?? 1,
      baseUnit: res?.baseUnit ?? '',
    });
  }
}
