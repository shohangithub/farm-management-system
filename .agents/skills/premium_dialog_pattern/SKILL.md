---
name: premium_dialog_pattern
description: Standardized design patterns for Angular Material dialog components, ensuring premium UI aesthetics, consistent form handling, and error displaying in the Farm360 application.
---

# Premium Dialog Design Pattern

When creating or refactoring a dialog component (e.g., popup forms), ALWAYS adhere to this exact structural and visual standard.

## 1. Overall Container
Wrap the dialog content in a glassmorphic/premium card container with a fixed maximum height and rounded corners:
```html
<div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
  ...
</div>
```

## 2. Header
Use a standard header with a flat background, a leading icon, title, short description, and a close button:
```html
<div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
  <div>
    <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
      <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">icon_name</mat-icon>
      Dialog Title
    </h2>
    <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Short description of the task</p>
  </div>
  <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
    <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
  </button>
</div>
```

## 3. Form and Error Handling
The form wraps the content body and footer. Include an error state banner at the top of the form.
```html
<form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
  
  <!-- Error State -->
  <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm whitespace-pre-wrap flex items-start gap-2">
    <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
    <span>{{ error() }}</span>
  </div>
  
  <!-- Content Body -->
</form>
```

## 4. Scrollable Body
The main content area flexes to fill available space and overflows with a custom slim scrollbar:
```html
<div class="p-6 space-y-4 overflow-y-auto custom-scrollbar flex-1">
  <!-- Inputs Go Here -->
</div>
```
Include these styles in the component class:
```typescript
styles: [`
  .custom-scrollbar::-webkit-scrollbar { width: 6px; }
  .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
  .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.5); border-radius: 20px; }
  .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.8); }
`]
```

## 5. Standard Form Inputs
Inputs and selects must use matching borders, focused rings, and background transitions. Labels should be small, bold, and uppercase tracking wider.
```html
<div class="space-y-1.5">
  <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
    Field Label <span class="text-red-500">*</span>
  </label>
  <input type="text" formControlName="fieldName" placeholder="..."
         class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
</div>
```

## 6. Footer Actions
The footer anchors to the bottom with an animated primary save button and a grey cancel button.
```html
<div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
  <button type="button" mat-dialog-close [disabled]="isLoading()"
    class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
    Cancel
  </button>
  <button type="submit" [disabled]="form.invalid || isLoading()"
          class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
    <mat-icon *ngIf="isLoading()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
    <span>{{ isLoading() ? 'Saving...' : 'Save' }}</span>
  </button>
</div>
```

## 7. Component TypeScript
- Use **Signals** for local UI state (`isLoading = signal(false)`, `error = signal('')`).
- Utilize `parseApiError` from the utility folder for consistent API error parsing.
- Always implement `ChangeDetectionStrategy.OnPush`.

## 8. Delete Functionality (List Pages)
Delete functionality should **NOT** be placed inside the setup/edit dialog itself. Instead, it should be placed directly on the list page (e.g., as an action icon on a grid card or data table row). 

When implementing a delete action, NEVER use the native browser `confirm()`. Instead, use the shared `ConfirmationDialogComponent`.

```typescript
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';

// ... inside your component class
onDelete(entity: EntityDto, event: Event): void {
  event.stopPropagation(); // If the button is inside a clickable card/row
  
  const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
    width: '450px',
    panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
    data: {
      title: 'Delete Entity',
      message: `Are you sure you want to delete "${entity.name}"? This action cannot be undone.`,
      confirmButtonText: 'Delete',
      cancelButtonText: 'Cancel',
      isDestructive: true // Highlights the confirm button in red
    }
  });

  dialogRef.afterClosed().subscribe(confirmed => {
    if (confirmed) {
      this.entityService.deleteEntity(entity.id).subscribe({
        next: () => this.reloadList(),
        error: (err) => {
          console.error('Failed to delete entity', err);
          // Handle error (e.g., via a toast notification service)
        }
      });
    }
  });
}
```
