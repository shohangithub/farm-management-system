import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { InventoryService } from '../../services/inventory.service';
import { MeasureUnit } from '../../models/inventory.models';

interface BaseUnitForm { id: string | null; name: string; description: string; isActive: boolean; }

/**
 * Base Units (as in FrostTrack): the unit each item's stock is counted in — kg, g, litre, ml,
 * piece. Unit Name, Description and Status, with add, edit and delete (one or many).
 * Packs such as "Bag (50 kg)" are kept on the Unit Conversions page.
 */
@Component({
  selector: 'app-base-units',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, PageHeaderComponent],
  template: `
    <app-page-header title="Base Units · মূল একক" description="The units stock is counted in. Bags, bottles and other packs are set up under Unit Conversions."
      breadcrumbActiveNode="Base Units">
      <div actions class="flex gap-2">
        <a mat-stroked-button routerLink="/inventory/unit-conversions" class="!rounded-xl !text-xs"><mat-icon class="!text-[16px]">swap_horiz</mat-icon> Unit Conversions</a>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-6xl">
      <div class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 overflow-hidden">
        <!-- Toolbar: search, add, delete selected -->
        <div class="p-4 flex flex-wrap items-center gap-2 border-b border-gray-100 dark:border-gray-700">
          <div class="relative">
            <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 !text-[18px] !w-[18px] !h-[18px] leading-none text-gray-400 pointer-events-none">search</mat-icon>
            <input [ngModel]="search()" (ngModelChange)="search.set($event)" placeholder="Search" class="field search w-64" />
          </div>
          <button (click)="add()" title="Add" class="w-9 h-9 rounded-full bg-emerald-600 text-white flex items-center justify-center shadow-sm hover:bg-emerald-700">
            <mat-icon class="!text-[20px]">add</mat-icon>
          </button>
          <button *ngIf="selected().length" (click)="removeSelected()" title="Delete selected" class="w-9 h-9 rounded-full bg-rose-600 text-white flex items-center justify-center shadow-sm hover:bg-rose-700">
            <mat-icon class="!text-[20px]">delete</mat-icon>
          </button>
          <button (click)="load()" title="Refresh" class="w-9 h-9 rounded-full border border-gray-300 dark:border-gray-600 text-gray-600 flex items-center justify-center hover:bg-gray-50">
            <mat-icon class="!text-[20px]">refresh</mat-icon>
          </button>
          <span class="ml-auto text-xs text-gray-500">{{ rows().length }} unit(s){{ selected().length ? ' · ' + selected().length + ' selected' : '' }}</span>
        </div>

        <div *ngIf="error()" class="m-4 p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 text-sm text-rose-700 flex justify-between">
          <span>{{ error() }}</span><button (click)="error.set(null)"><mat-icon class="!text-[16px]">close</mat-icon></button>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
              <tr>
                <th class="px-4 py-2.5 w-10"><input type="checkbox" class="accent-emerald-600" [checked]="allSelected()" (change)="toggleAll()" /></th>
                <th class="text-left px-3 py-2.5">Unit Name</th>
                <th class="text-left px-3 py-2.5">Description</th>
                <th class="text-left px-3 py-2.5">Packs</th>
                <th class="text-left px-3 py-2.5 w-28">Status</th>
                <th class="text-right px-4 py-2.5 w-28">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngIf="!rows().length"><td colspan="6" class="px-4 py-10 text-center text-gray-400">No base units.</td></tr>
              <tr *ngFor="let u of rows()" class="border-t border-gray-100 dark:border-gray-700/60 hover:bg-gray-50/60 dark:hover:bg-gray-700/20">
                <td class="px-4 py-2.5"><input type="checkbox" class="accent-emerald-600" [checked]="selected().includes(u.id)" (change)="toggle(u.id)" /></td>
                <td class="px-3 py-2.5 font-semibold">{{ u.name }}</td>
                <td class="px-3 py-2.5 text-gray-600 dark:text-gray-300">{{ u.description || '—' }}</td>
                <td class="px-3 py-2.5 text-xs text-gray-500">{{ packs(u) || '—' }}</td>
                <td class="px-3 py-2.5">
                  <span class="px-2 py-0.5 rounded-full text-[11px] font-bold" [ngClass]="u.isActive ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300' : 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300'">
                    {{ u.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
                <td class="px-4 py-2.5 text-right whitespace-nowrap">
                  <button (click)="edit(u)" title="Edit" class="p-1 text-gray-400 hover:text-emerald-600"><mat-icon class="!text-[18px]">edit</mat-icon></button>
                  <button (click)="remove(u)" title="Delete" class="p-1 text-gray-400 hover:text-rose-600"><mat-icon class="!text-[18px]">delete</mat-icon></button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Add / Edit -->
    <div *ngIf="form() as f" class="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" (click)="form.set(null)">
      <div class="bg-white dark:bg-gray-800 rounded-2xl w-full max-w-md overflow-hidden" (click)="$event.stopPropagation()">
        <div class="px-5 py-4 border-b border-gray-100 dark:border-gray-700 flex justify-between items-center">
          <h3 class="text-base font-black m-0">{{ f.id ? 'Edit' : 'Add' }} Base Unit</h3>
          <button (click)="form.set(null)" class="text-gray-400"><mat-icon>close</mat-icon></button>
        </div>
        <div class="p-5 space-y-3 text-sm">
          <label class="flex flex-col gap-1 font-semibold text-xs">Unit Name <span class="text-rose-500 -mt-1">*</span>
            <input [(ngModel)]="f.name" placeholder="e.g. kg" class="field" />
          </label>
          <label class="flex flex-col gap-1 font-semibold text-xs">Description
            <input [(ngModel)]="f.description" placeholder="e.g. Kilogram — feed and grain" class="field" />
          </label>
          <label class="flex flex-col gap-1 font-semibold text-xs">Status <span class="text-rose-500 -mt-1">*</span>
            <select [(ngModel)]="f.isActive" class="field">
              <option [ngValue]="true">Active</option>
              <option [ngValue]="false">Inactive</option>
            </select>
          </label>
        </div>
        <div class="px-5 py-3 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-2">
          <button mat-stroked-button (click)="form.set(null)" class="!rounded-xl">Cancel</button>
          <button mat-flat-button (click)="save()" [disabled]="busy() || !f.name.trim()" class="!rounded-xl !bg-emerald-600 !text-white">{{ f.id ? 'Update' : 'Save' }}</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .field { padding: 0.5rem 0.75rem; border-radius: 0.6rem; border: 1px solid rgb(209 213 219); background: white; font-size: 0.8125rem; font-weight: 400; color: rgb(17 24 39); outline: none; }
    .field:focus { border-color: rgb(16 185 129); }
    .field.search { padding-left: 2.25rem; }
    :host-context(.dark) .field { background: rgb(17 24 39); border-color: rgb(55 65 81); color: white; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BaseUnitsComponent {
  private readonly inventory = inject(InventoryService);

  readonly units = signal<MeasureUnit[]>([]);
  readonly search = signal('');
  readonly selected = signal<string[]>([]);
  readonly form = signal<BaseUnitForm | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly rows = computed(() => {
    const term = this.search().trim().toLowerCase();
    return this.units().filter(u => !term
      || u.name.toLowerCase().includes(term)
      || (u.description ?? '').toLowerCase().includes(term));
  });

  readonly allSelected = computed(() => this.rows().length > 0 && this.rows().every(r => this.selected().includes(r.id)));

  constructor() {
    this.load();
  }

  load(): void {
    this.inventory.getUnits().subscribe({
      next: u => { this.units.set(u); this.selected.set([]); },
      error: err => this.error.set(parseApiError(err, 'Failed to load base units.')),
    });
  }

  packs(u: MeasureUnit): string {
    return u.conversions.filter(c => !c.isBase).map(c => c.name).join(', ');
  }

  toggle(id: string): void {
    this.selected.update(s => s.includes(id) ? s.filter(x => x !== id) : [...s, id]);
  }

  toggleAll(): void {
    const ids = this.rows().map(r => r.id);
    this.selected.set(this.allSelected() ? [] : ids);
  }

  add(): void {
    this.form.set({ id: null, name: '', description: '', isActive: true });
  }

  edit(u: MeasureUnit): void {
    this.form.set({ id: u.id, name: u.name, description: u.description ?? '', isActive: u.isActive });
  }

  save(): void {
    const f = this.form();
    if (!f) return;
    this.busy.set(true);
    this.inventory.saveUnit({ id: f.id, name: f.name.trim(), description: f.description || null, isActive: f.isActive }).subscribe({
      next: () => { this.busy.set(false); this.form.set(null); this.error.set(null); this.load(); },
      error: err => { this.busy.set(false); this.error.set(parseApiError(err, 'Could not save the base unit.')); },
    });
  }

  remove(u: MeasureUnit): void {
    if (!confirm(`Delete base unit "${u.name}" and its conversions?`)) return;
    this.delete([u.id]);
  }

  removeSelected(): void {
    if (!confirm(`Delete ${this.selected().length} base unit(s) and their conversions?`)) return;
    this.delete(this.selected());
  }

  private delete(ids: string[]): void {
    this.busy.set(true);
    this.inventory.deleteUnits(ids).subscribe({
      next: () => { this.busy.set(false); this.error.set(null); this.load(); },
      error: err => { this.busy.set(false); this.error.set(parseApiError(err, 'Could not delete.')); },
    });
  }
}
