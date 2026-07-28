# Farm360 AI Agent Instructions & Frontend Architecture Rules

Whenever modifying or creating Angular frontend components for the Farm360 project, adhere to these strict constraints:

## 1. Absolute Prohibition of `ChangeDetectorRef`
- **Rule**: NEVER inject or use `ChangeDetectorRef` (`detectChanges()`, `markForCheck()`).
- **Reason**: The project uses an active reactivity model based on Signals. If the view does not update, the RxJS-to-Signal pipeline is flawed. Fix the pipeline instead of forcing manual reflows.

## 2. Default to `OnPush` Change Detection
- **Rule**: ALL new components MUST include `changeDetection: ChangeDetectionStrategy.OnPush` in their `@Component` decorator.

## 3. Strict Signal-Based Reactivity
- **Rule**: Do not use imperative state bindings. Instead of standard properties (`isLoading = false`), state MUST be represented as Signals (`isLoading = signal(false)`).
- **Rule**: Convert HTTP observables to Signals using `toSignal()` from `@angular/core/rxjs-interop`.
- **Rule**: Combine dependencies declaratively using `computed()`.

## 4. RxJS Memory Safety
- **Rule**: If you must manually call `.subscribe()` (e.g., for form changes), you MUST use `takeUntilDestroyed(this.destroyRef)` to prevent memory leaks.

## 5. Parallel Data Fetching
- **Rule**: Do not perform sequential "waterfall" subscriptions. If a component needs multiple independent data sources upon loading, use `forkJoin` combined with `switchMap` inside the declarative pipeline.

## 6. Premium UI Aesthetics
- **Rule**: Always adhere to the premium design standard. Utilize existing Tailwind semantic classes, vibrant micro-animations, loading states (spinners), and informative "empty states" when data is unavailable.

## 7. Mandated UI Component & Layout Pattern Consistency
- **Page Headers**: All feature pages MUST use `<app-page-header>` from `@shared/components/page-header` with `title`, `description`, `breadcrumbActiveNode`, and `<div actions>` button slots.
- **Loading Overlays**: All card containers and tables MUST use `<app-loading *ngIf="isLoading()" [overlay]="true">` from `@shared/components/loading`.
- **Empty States**: All pages MUST use `<app-empty-state *ngIf="!isLoading() && (!items || items.length === 0)">` from `@shared/components/empty-state` with an icon, title, description, and action listener.
- **Glassmorphic Containers**: Main content wrappers MUST use standard glassmorphic card containers: `bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative`.
- **Grid Cards**: Grid card layouts MUST include gradient icon badges (`bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20`), background watermarks (`mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none"`), and rounded action buttons.
- **Modal Dialog Design**: Dialogs MUST use rounded corners (`rounded-2xl`), a dedicated header with icon badge and close button, an RFC 7807 error banner (`parseApiError`), and a styled action footer with Cancel / Primary Action buttons.
- **Payload Sanitization**: Form submit handlers MUST sanitize empty string fields (`""`) to `null` before sending HTTP requests to prevent `BadHttpRequestException` deserialization errors.

