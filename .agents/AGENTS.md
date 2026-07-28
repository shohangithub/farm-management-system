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
