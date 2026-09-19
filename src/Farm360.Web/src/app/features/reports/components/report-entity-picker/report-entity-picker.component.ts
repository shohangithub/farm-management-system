import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import {
  Observable,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  startWith,
  switchMap,
} from 'rxjs';
import { AnimalService } from '../../../livestock/services/animal.service';
import { BatchService } from '../../../livestock/services/batch.service';
import { BreedService } from '../../../livestock/services/breed.service';
import { AnimalDto, AnimalListItemDto } from '../../../livestock/models/animal.models';
import { BatchDto } from '../../../livestock/models/batch.models';
import { BreedDto } from '../../../livestock/models/breed.models';
import { FarmService } from '../../../farms/services/farm.service';
import { ShedService } from '../../../farms/services/shed.service';
import { ShedList } from '../../../farms/models/shed.model';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { ReportParameterType } from '../../models/report.models';

interface PickerOption {
  id: string;
  label: string;
  sublabel?: string;
}

/**
 * Renders a report's Animal / Batch / Farm / Shed / Breed parameter as a dropdown (or, for
 * Animal, a searchable autocomplete) instead of a raw GUID text box.
 *
 * The report platform is metadata-driven -- a report declares "this parameter is an Animal" and
 * has no idea how that gets presented -- so the mapping from parameter type to a live,
 * farm-scoped list belongs here, once, rather than inside every report page that happens to
 * take an animal.
 */
@Component({
  selector: 'app-report-entity-picker',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatFormFieldModule, MatSelectModule,
    MatAutocompleteModule, MatInputModule, MatIconModule, MatProgressSpinnerModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (isSearchable()) {
      <!-- Animal: the herd can run into the thousands, so this searches server-side rather
           than trying to fit every animal into one dropdown. -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full-width">
        <mat-label>{{ label() }}{{ required() ? ' *' : '' }}</mat-label>
        <input matInput
               [formControl]="searchControl"
               [matAutocomplete]="auto"
               [placeholder]="scoped() ? 'Type a tag or breed to search' : 'Select a farm first'" />
        @if (loading()) {
          <mat-spinner matSuffix diameter="18"></mat-spinner>
        } @else if (selected()) {
          <button matSuffix type="button" class="clear-btn" (click)="clear($event)" aria-label="Clear">
            <mat-icon>close</mat-icon>
          </button>
        }
        <mat-autocomplete #auto="matAutocomplete" [displayWith]="displaySelected"
                           (optionSelected)="onAnimalSelected($event)">
          @for (option of animalOptions(); track option.id) {
            <mat-option [value]="option">
              <div class="flex items-center justify-between w-full">
                <span class="opt-main text-sm font-medium text-gray-900 dark:text-white">{{ option.label }}</span>
                @if (option.sublabel) {
                  <span class="opt-sub text-xs text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/40 px-2 py-0.5 rounded-full font-medium">{{ option.sublabel }}</span>
                }
              </div>
            </mat-option>
          }
          @if (!loading() && scoped() && !searchFailed() && animalOptions().length === 0) {
            <mat-option disabled>
              <span class="text-xs text-gray-500">No matching animals</span>
            </mat-option>
          }
          @if (searchFailed()) {
            <mat-option disabled class="error-option">
              <span class="text-xs text-red-500">Could not load animals -- try again</span>
            </mat-option>
          }
        </mat-autocomplete>
        @if (!scoped()) {
          <mat-hint>Choose a farm above, or in the top bar, to search its animals.</mat-hint>
        }
      </mat-form-field>
    } @else {
      <!-- Farm / Batch / Shed / Breed: bounded lists a plain dropdown handles comfortably. -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full-width">
        <mat-label>{{ label() }}{{ required() ? ' *' : '' }}</mat-label>
        <mat-select [value]="value()" [disabled]="!scoped() && needsFarmScope()"
                    (selectionChange)="valueChange.emit($event.value)">
          @if (!required()) {
            <mat-option [value]="null">-- None --</mat-option>
          }
          @for (option of listOptions(); track option.id) {
            <mat-option [value]="option.id">
              {{ option.label }}{{ option.sublabel ? ' -- ' + option.sublabel : '' }}
            </mat-option>
          }
        </mat-select>
        @if (!scoped() && needsFarmScope()) {
          <mat-hint>Choose a farm above, or in the top bar, first.</mat-hint>
        } @else if (listFailed()) {
          <mat-hint class="error-hint">Could not load the list -- try again.</mat-hint>
        } @else if (!loading() && listOptions().length === 0) {
          <mat-hint>Nothing found for this farm.</mat-hint>
        }
      </mat-form-field>
    }
  `,
  styles: [`
    .full-width { width: 100%; }
    .opt-main { font-weight: 500; }
    .opt-sub { margin-left: 6px; font-size: 12px; }
    .clear-btn {
      border: none;
      background: none;
      cursor: pointer;
      opacity: .6;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      padding: 0;
      margin-right: 4px;
    }
    .clear-btn:hover { opacity: 1; }
    .clear-btn mat-icon { font-size: 18px; width: 18px; height: 18px; line-height: 18px; }
    mat-spinner { margin-right: 4px; }
    .error-option, .error-hint { color: var(--mat-sys-error, #b3261e); }
  `],
})
export class ReportEntityPickerComponent {
  private readonly animals = inject(AnimalService);
  private readonly batches = inject(BatchService);
  private readonly breeds = inject(BreedService);
  private readonly farms = inject(FarmService);
  private readonly sheds = inject(ShedService);
  private readonly workingContext = inject(WorkingContextService);
  private readonly destroyRef = inject(DestroyRef);

  readonly entityType = input.required<ReportParameterType>();
  readonly value = input<string | null>(null);
  readonly label = input('');
  readonly required = input(false);

  /**
   * The farm this picker is scoped to. Comes from a sibling "Farm" parameter when the report
   * declares one (e.g. the herd-performance report); otherwise falls back to whichever farm is
   * active in the app's own farm switcher, so an animal-only report (e.g. the feeding report)
   * still searches the right herd without asking the user to pick a farm twice.
   */
  readonly farmId = input<string | null>(null);

  readonly valueChange = output<string | null>();

  protected readonly searchControl = new FormControl<string | PickerOption>('');
  protected readonly loading = signal(false);
  protected readonly animalOptions = signal<PickerOption[]>([]);
  protected readonly listOptions = signal<PickerOption[]>([]);
  protected readonly selected = signal<PickerOption | null>(null);
  protected readonly searchFailed = signal(false);
  protected readonly listFailed = signal(false);

  private lastScopedFarmId: string | null = null;

  /**
   * A genuine Signal over WorkingContextService.currentFarm$, not a read of its plain
   * `currentFarmValue` getter. A `computed()` only re-runs when a *Signal* it read changes;
   * reading a plain BehaviorSubject-backed getter registers no dependency, so a computed built
   * on it would freeze at whatever the farm was at the computed's first evaluation and never
   * notice the org/branch/farm context resolving later (a real risk on a cold page load, where
   * that chain is still mid-flight when this component is constructed).
   */
  private readonly activeFarm = toSignal(this.workingContext.currentFarm$, {
    initialValue: this.workingContext.currentFarmValue,
  });

  private readonly effectiveFarmId = computed(() => this.farmId() ?? this.activeFarm()?.id ?? null);

  protected readonly isSearchable = computed(() => this.entityType() === 'Animal');
  protected readonly needsFarmScope = computed(() =>
    this.entityType() === 'Animal' || this.entityType() === 'Batch' || this.entityType() === 'Shed');
  protected readonly scoped = computed(() => !this.needsFarmScope() || !!this.effectiveFarmId());

  constructor() {
    // Keep the displayed selection in sync when the parent resets or pre-fills the value
    // (e.g. restoring a remembered parameter set or receiving query params).
    effect(() => {
      const currentValue = this.value();

      if (!currentValue) {
        this.selected.set(null);
        if (this.isSearchable()) {
          this.searchControl.setValue('', { emitEvent: false });
        }
        return;
      }

      // If current selected option already matches the target ID, keep it and ensure searchControl is synced
      if (this.selected()?.id === currentValue) {
        if (this.isSearchable() && this.searchControl.value !== this.selected()) {
          this.searchControl.setValue(this.selected()!, { emitEvent: false });
        }
        return;
      }

      const currentOptions = [...this.animalOptions(), ...this.listOptions()];
      const match = currentOptions.find(o => o.id === currentValue) ?? null;

      if (match) {
        this.selected.set(match);
        if (this.isSearchable()) {
          this.searchControl.setValue(match, { emitEvent: false });
        }
      } else if (this.isSearchable()) {
        // Option is set by parent (e.g. URL query param or remembered assumption) but not in loaded options list:
        // Fetch the animal by ID so its label/tag displays properly in the input.
        this.animals.getById(currentValue).subscribe({
          next: (animal: AnimalDto) => {
            const opt = this.toOption(animal);
            this.selected.set(opt);
            this.searchControl.setValue(opt, { emitEvent: false });
            this.animalOptions.update(opts =>
              opts.some(o => o.id === opt.id) ? opts : [opt, ...opts],
            );
          },
          error: () => {
            this.selected.set(null);
            this.searchControl.setValue('', { emitEvent: false });
          },
        });
      }
    });

    // Mirror scoped() onto the FormControl's own enabled state via enable()/disable() rather
    // than a template [disabled] binding: Angular's reactive-forms directives own the control's
    // disabled state once [formControl] is attached.
    effect(() => {
      if (this.scoped()) {
        this.searchControl.enable({ emitEvent: false });
      } else {
        this.searchControl.disable({ emitEvent: false });
      }
    });

    // Bounded lists (Farm, Breed, Batch, Shed) -- loaded whenever the entity type or its farm
    // scope changes, and cleared while no farm is selected for the farm-scoped ones.
    combineLatest([toObservable(this.entityType), toObservable(this.effectiveFarmId)])
      .pipe(
        switchMap(([type, farmId]) =>
          this.loadBoundedList(type, farmId).pipe(
            map(options => ({ options, failed: false })),
            catchError(err => {
              console.error(`[report-entity-picker] failed to load ${type} list`, err);
              return of({ options: [] as PickerOption[], failed: true });
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ options, failed }) => {
        this.listOptions.set(options);
        this.listFailed.set(failed);
        const currentValue = this.value();
        this.selected.set(options.find(o => o.id === currentValue) ?? null);
      });

    // Animal search -- debounced, cancels previous request on every keystroke, and is
    // re-run whenever the farm scope changes so switching farms clears stale results.
    combineLatest([
      toObservable(this.effectiveFarmId),
      this.searchControl.valueChanges.pipe(startWith('')),
    ])
      .pipe(
        debounceTime(250),
        distinctUntilChanged(
          (a, b) => a[0] === b[0] && this.searchTermOf(a[1]) === this.searchTermOf(b[1]),
        ),
        switchMap(([farmId, term]) => {
          if (!farmId) {
            return of({ options: [] as PickerOption[], failed: false });
          }

          // If farm scope genuinely changed, reset previous selection
          if (this.lastScopedFarmId !== null && this.lastScopedFarmId !== farmId) {
            this.selected.set(null);
            this.searchControl.setValue('', { emitEvent: false });
            this.valueChange.emit(null);
          }
          this.lastScopedFarmId = farmId;

          // When term is an object (PickerOption), the user clicked an autocomplete option or
          // it was set programmatically. Do NOT perform an API search and do NOT wipe options.
          if (typeof term !== 'string') {
            const current = this.animalOptions();
            const exists = current.some(o => o.id === (term as PickerOption).id);
            const updated = exists ? current : [term as PickerOption, ...current];
            return of({ options: updated, failed: false });
          }

          // If the user erased all text while an animal was previously selected, clear selection
          if (term.trim() === '' && this.selected() !== null) {
            this.selected.set(null);
            this.valueChange.emit(null);
          }

          this.loading.set(true);
          return this.animals
            .getList({ farmId, search: term.trim(), pageSize: 20 })
            .pipe(
              map(res => {
                const items = res.items.map(a => this.toOption(a));
                const currentSelected = this.selected();
                if (currentSelected && !items.some(o => o.id === currentSelected.id)) {
                  return { options: [currentSelected, ...items], failed: false };
                }
                return { options: items, failed: false };
              }),
              catchError(err => {
                console.error('[report-entity-picker] animal search failed', err);
                return of({ options: [] as PickerOption[], failed: true });
              }),
            );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ options, failed }) => {
        this.animalOptions.set(options);
        this.searchFailed.set(failed);
        this.loading.set(false);
      });
  }

  protected onAnimalSelected(event: MatAutocompleteSelectedEvent): void {
    const option = event.option.value as PickerOption;
    this.selected.set(option);
    this.animalOptions.update(opts =>
      opts.some(o => o.id === option.id) ? opts : [option, ...opts],
    );
    this.valueChange.emit(option.id);
  }

  protected clear(event: Event): void {
    event.stopPropagation();
    this.selected.set(null);
    this.searchControl.setValue('');
    this.valueChange.emit(null);
  }

  protected displaySelected = (option: PickerOption | string | null): string => {
    if (!option) return '';
    return typeof option === 'string' ? option : (option.label ?? '');
  };

  private searchTermOf(value: string | PickerOption | null): string {
    if (value === null || value === undefined) {
      return '';
    }
    return typeof value === 'string' ? value : value.label;
  }

  private toOption(animal: AnimalListItemDto | AnimalDto): PickerOption {
    return { id: animal.id, label: animal.tagId, sublabel: animal.breedName };
  }

  private loadBoundedList(type: ReportParameterType, farmId: string | null): Observable<PickerOption[]> {
    switch (type) {
      case 'Farm':
        return this.workingContext.farms$.pipe(
          map(list => list.map(f => ({ id: f.id, label: f.name }))),
        );

      case 'Breed':
        return this.breeds.getBreeds({ pageSize: 500 }).pipe(
          map(res => res.items.map((b: BreedDto) => ({ id: b.id, label: b.name, sublabel: b.mainPurpose }))),
          catchError(() => of<PickerOption[]>([])),
        );

      case 'Batch':
        if (!farmId) {
          return of<PickerOption[]>([]);
        }
        return this.batches.getBatches(farmId, undefined, 1, 200).pipe(
          map(res => res.items.map((b: BatchDto) => ({ id: b.id, label: b.name, sublabel: `${b.animalCount} head` }))),
          catchError(() => of<PickerOption[]>([])),
        );

      case 'Shed':
        if (!farmId) {
          return of<PickerOption[]>([]);
        }
        return this.sheds.getShedsByFarm(farmId).pipe(
          map(res => res.map((s: ShedList) => ({ id: s.id, label: s.shedName, sublabel: s.shedNumber }))),
          catchError(() => of<PickerOption[]>([])),
        );

      default:
        return of<PickerOption[]>([]);
    }
  }
}
