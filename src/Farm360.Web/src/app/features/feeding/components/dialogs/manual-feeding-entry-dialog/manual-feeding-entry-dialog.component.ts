import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { toSignal } from '@angular/core/rxjs-interop';
import { FeedingService } from '../../../services/feeding.service';
import { InventoryService } from '../../../../inventory/services/inventory.service';
import { InventoryCategory, InventoryItem } from '../../../../inventory/models/inventory.models';
import { FeedFormula, ManualFeedingResult } from '../../../models/feeding.models';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';
import { parseApiError } from '../../../../../core/utils/error-parser';

export interface ManualFeedingDialogData {
  farmId: string;
  date: string;
  animalIds?: string[];
}

/** Records feed actually given outside the plan — confirmed at once, so stock, cost and reports follow. */
@Component({
  selector: 'app-manual-feeding-entry-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule, AnimalPickerComponent],
  styles: [`
    .field { width: 100%; padding: 0.5rem 0.75rem; border-radius: 0.6rem; border: 1px solid rgb(209 213 219); background: white; font-size: 0.8125rem; color: rgb(17 24 39); outline: none; }
    .field:focus { border-color: rgb(16 185 129); box-shadow: 0 0 0 3px rgba(16, 185, 129, .15); }
    :host-context(.dark) .field { background: rgb(17 24 39); border-color: rgb(55 65 81); color: white; }
  `],
  template: `
    <div class="flex flex-col max-h-[92vh] bg-gray-50 dark:bg-gray-900 rounded-2xl overflow-hidden">
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700 shrink-0 flex justify-between items-center">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-500 to-orange-600 text-white flex items-center justify-center">
            <mat-icon class="!w-5 !h-5 !text-[20px]">edit_note</mat-icon>
          </div>
          <div>
            <h2 class="text-lg font-bold text-gray-900 dark:text-white leading-tight m-0">Manual Feed Entry <span class="text-sm font-normal text-gray-400">· হাতে খাবার এন্ট্রি</span></h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 m-0">Feed given outside the plan — saved as confirmed</p>
          </div>
        </div>
        <button mat-icon-button (click)="dialogRef.close()" class="text-gray-400"><mat-icon>close</mat-icon></button>
      </div>

      <form [formGroup]="form" class="flex-1 overflow-y-auto p-6 space-y-4 text-xs">
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <label class="block">
            <span class="font-semibold text-gray-700 dark:text-gray-300">Date fed</span>
            <input type="date" class="field mt-1" formControlName="date" [max]="today" />
          </label>
          <label class="block">
            <span class="font-semibold text-gray-700 dark:text-gray-300">{{ isItem() ? 'Quantity per animal' : 'Kg per animal' }}</span>
            <div class="relative mt-1">
              <input type="number" step="0.01" min="0" class="field pr-12" formControlName="quantityPerHead" placeholder="0" />
              <span class="absolute right-2 top-1/2 -translate-y-1/2 text-[11px] font-bold text-gray-400">{{ unitLabel() }}</span>
            </div>
          </label>
        </div>

        <label class="block">
          <span class="font-semibold text-gray-700 dark:text-gray-300">What was fed</span>
          <select class="field mt-1" formControlName="source">
            <option value="">— choose a recipe or stock item —</option>
            <optgroup label="Recipes">
              <option *ngFor="let f of formulas()" [value]="'f:' + f.id">{{ f.title }}</option>
            </optgroup>
            <optgroup label="Stock items (fed as they are)">
              <option *ngFor="let it of stockItems()" [value]="'i:' + it.id">{{ it.name }} — {{ it.currentStock | number:'1.0-2' }} {{ it.unitOfMeasure }} in stock</option>
            </optgroup>
          </select>
        </label>

        <div>
          <span class="font-semibold text-gray-700 dark:text-gray-300">Animals</span>
          <div class="mt-1"><app-animal-picker formControlName="animalIds" [multiSelect]="true"></app-animal-picker></div>
          <p class="text-[11px] text-gray-500 mt-1">{{ count() }} selected</p>
        </div>

        <label class="block">
          <span class="font-semibold text-gray-700 dark:text-gray-300">Note <span class="font-normal text-gray-400">(optional)</span></span>
          <input type="text" class="field mt-1" formControlName="notes" maxlength="300" placeholder="e.g. Morning feed missed in the plan" />
        </label>

        <div *ngIf="total() > 0" class="rounded-xl border border-emerald-200 dark:border-emerald-800 bg-emerald-50 dark:bg-emerald-950/30 px-4 py-3 text-emerald-800 dark:text-emerald-300">
          <b>{{ total() | number:'1.0-3' }} {{ unitLabel() }}</b> in all ({{ count() }} × {{ form.value.quantityPerHead }}) will be taken from stock
          and charged to these animals on {{ form.value.date | date:'dd MMM yyyy' }}.
        </div>
      </form>

      <div class="px-6 py-3 bg-white dark:bg-gray-800 border-t border-gray-100 dark:border-gray-700 shrink-0 flex justify-end gap-2">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="button" (click)="save()" [disabled]="form.invalid || saving()">
          {{ saving() ? 'Saving…' : 'Save & confirm' }}
        </button>
      </div>
    </div>
  `
})
export class ManualFeedingEntryDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<ManualFeedingEntryDialogComponent, ManualFeedingResult>);
  private readonly data = inject<ManualFeedingDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly feedingService = inject(FeedingService);
  private readonly inventoryService = inject(InventoryService);
  private readonly snackBar = inject(MatSnackBar);

  readonly today = new Date().toISOString().split('T')[0];
  readonly formulas = signal<FeedFormula[]>([]);
  readonly stockItems = signal<InventoryItem[]>([]);
  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group({
    date: [this.data.date > this.today ? this.today : this.data.date, Validators.required],
    source: ['', Validators.required],
    quantityPerHead: [null as number | null, [Validators.required, Validators.min(0.001)]],
    animalIds: [this.data.animalIds ?? [] as string[], Validators.required],
    notes: ['']
  });

  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  readonly count = computed(() => (this.value().animalIds ?? []).length);
  readonly isItem = computed(() => (this.value().source ?? '').startsWith('i:'));
  readonly unitLabel = computed(() => {
    const src = this.value().source ?? '';
    return src.startsWith('i:') ? (this.stockItems().find(i => i.id === src.slice(2))?.unitOfMeasure ?? '') : 'kg';
  });
  readonly total = computed(() => this.count() * (Number(this.value().quantityPerHead) || 0));

  ngOnInit(): void {
    this.feedingService.getFormulas(1, 500).subscribe(res =>
      this.formulas.set(res.items.filter(f => !f.title.endsWith('(direct item)'))));
    this.inventoryService.getItems({ farmId: this.data.farmId, pageSize: 500 }).subscribe(res =>
      this.stockItems.set(res.items.filter(i => ![InventoryCategory.Equipment, InventoryCategory.Medicine, InventoryCategory.Vaccine].includes(i.category))));
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const id = v.source.slice(2);
    this.saving.set(true);
    this.feedingService.recordManualFeeding({
      farmId: this.data.farmId,
      date: v.date,
      animalIds: v.animalIds,
      formulaId: v.source.startsWith('f:') ? id : null,
      inventoryItemId: v.source.startsWith('i:') ? id : null,
      quantityPerHead: Number(v.quantityPerHead),
      notes: v.notes || null
    }).subscribe({
      next: res => this.dialogRef.close(res),
      error: err => {
        this.saving.set(false);
        this.snackBar.open(parseApiError(err, 'Could not record the feed.'), 'Close', { duration: 6000 });
      }
    });
  }
}
