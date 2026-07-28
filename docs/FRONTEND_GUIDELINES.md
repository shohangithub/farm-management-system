# Farm360 Angular Frontend Guidelines & Patterns

This document outlines the standard architectural patterns, reactivity principles, and coding constraints for the Farm360 frontend application.

## 1. State Management & Reactivity (Signals)
We use Angular v16+ Signals for local state management and reactivity. **Do not use imperative state bindings or manual change detection.**

### Constraints
- **NO `ChangeDetectorRef`**: Do not inject `ChangeDetectorRef` to call `detectChanges()` or `markForCheck()`. If your view isn't updating, your reactivity pipeline is flawed.
- **NO Imperative Subscriptions for State**: Do not manually `.subscribe()` to observables just to set a local variable (e.g. `isLoading = false`). 
- **Use `toSignal()`**: Map HTTP requests and observables to Signals using `@angular/core/rxjs-interop`'s `toSignal()`.
- **Use `computed()`**: Derive state from other signals using `computed()` instead of recalculating state in methods.

### Correct Pattern Example
```typescript
// Good: Declarative reactivity using Signals
private fetchParams = computed(() => ({ id: this.id(), refresh: this.refreshTrigger() }));

readonly dataResult = toSignal(
  toObservable(this.fetchParams).pipe(
    tap(() => this.isLoading.set(true)),
    switchMap(params => this.apiService.get(params).pipe(
      catchError(() => of(null))
    )),
    tap(() => this.isLoading.set(false))
  ),
  { initialValue: null }
);

readonly item = computed(() => this.dataResult());
```

## 2. Change Detection
- **Enforce `OnPush`**: EVERY component must declare `changeDetection: ChangeDetectionStrategy.OnPush`. There are no exceptions.

## 3. Data Fetching
- **Parallel Requests**: When a page requires multiple datasets (e.g., a branch detail and its list of farms), always use `forkJoin` combined with `switchMap` instead of nesting subscriptions or making sequential waterfall requests.

### Correct Pattern Example
```typescript
switchMap(({ id }) => forkJoin({
  details: this.service.getDetails(id),
  children: this.service.getChildren(id)
}))
```

## 4. RxJS Subscriptions
If you absolutely must `.subscribe()` (e.g., for side effects like form value changes or routing), you **must** clean up the subscription to prevent memory leaks.
- **Use `takeUntilDestroyed()`**: Pass `takeUntilDestroyed(this.destroyRef)` inside your pipe before `.subscribe()`.

```typescript
private destroyRef = inject(DestroyRef);

ngOnInit() {
  this.form.valueChanges.pipe(
    takeUntilDestroyed(this.destroyRef)
  ).subscribe(val => { ... });
}
```

## 5. UI and UX Patterns
- **Tailwind Classes**: Use semantic classes and our existing utility ecosystem (`bg-surface-dark`, `primary-600`, etc.).
- **Loading States**: Always provide visual feedback during async operations (e.g., spinners, skeleton loaders).
- **Empty States**: Display beautifully styled empty states when lists/tables have no data. Do not just show a blank table.
- **Error Handling**: Catch API errors gracefully and bind them to an `error` signal to display contextual error banners in the UI.

By adhering to these standards, we ensure the Farm360 frontend remains performant, free of memory leaks, and architecturally consistent.
