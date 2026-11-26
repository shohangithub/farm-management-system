import { ChangeDetectionStrategy, Component, DestroyRef, EventEmitter, Input, Output, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface HealthFilterOption {
  value: string;
  label: string;
}

/**
 * The search box + status chips that sit above every health list, so all lists filter the same way.
 * Search is debounced; chips are optional.
 */
@Component({
  selector: 'app-health-filter-bar',
  standalone: true,
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<div class="flex flex-col sm:flex-row sm:items-center gap-3 mb-4 no-print">
  <label class="relative block w-full sm:max-w-xs">
    <span class="sr-only">{{ placeholder | t }}</span>
    <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 !text-[18px] !w-[18px] !h-[18px] text-gray-400 pointer-events-none">search</mat-icon>
    <input type="search" [value]="search" (input)="onInput($any($event.target).value)" [placeholder]="placeholder | t"
      class="w-full h-11 pl-9 pr-3 text-sm rounded-xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 text-gray-900 dark:text-white placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary-500/30 focus:border-primary-500">
  </label>

  @if (options.length) {
    <div class="flex gap-1.5 overflow-x-auto -mx-4 px-4 sm:mx-0 sm:px-0 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden" role="group" [attr.aria-label]="'Filter' | t">
      @for (opt of options; track opt.value) {
        <button type="button" (click)="selectedChange.emit(opt.value)" [attr.aria-pressed]="opt.value === selected"
          class="shrink-0 h-9 px-3.5 rounded-full text-[13px] font-semibold border transition-colors whitespace-nowrap"
          [class]="opt.value === selected
            ? 'bg-gray-900 border-gray-900 text-white dark:bg-white dark:border-white dark:text-gray-900'
            : 'bg-white dark:bg-gray-900 border-gray-200 dark:border-gray-800 text-gray-600 dark:text-gray-300 hover:border-gray-300'">
          {{ opt.label | t }}
        </button>
      }
    </div>
  }
</div>
  `
})
export class HealthFilterBarComponent {
  @Input() placeholder = 'Search';
  @Input() search = '';
  @Input() options: HealthFilterOption[] = [];
  @Input() selected = '';

  @Output() searchChange = new EventEmitter<string>();
  @Output() selectedChange = new EventEmitter<string>();

  private readonly input$ = new Subject<string>();

  constructor() {
    this.input$.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(v => this.searchChange.emit(v.trim()));
  }

  onInput(value: string): void {
    this.input$.next(value);
  }
}
