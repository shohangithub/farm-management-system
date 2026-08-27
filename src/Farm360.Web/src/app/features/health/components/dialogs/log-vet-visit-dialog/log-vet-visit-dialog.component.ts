import { Component, inject, DestroyRef, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { HealthService } from '../../../services/health.service';
import { WorkingContextService } from '../../../../../core/services/working-context.service';
import { parseApiError } from '../../../../../core/utils/error-parser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, map, startWith } from 'rxjs/operators';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-log-vet-visit-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, MatAutocompleteModule, MatInputModule],
  templateUrl: './log-vet-visit-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LogVetVisitDialog {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<LogVetVisitDialog>);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private destroyRef = inject(DestroyRef);

  isSubmitting = signal(false);
  error = signal('');
  knownVetNames = signal<string[]>([]);
  filteredVetNames$!: Observable<string[]>;

  farms$ = this.contextService.farms$;

  form = this.fb.group({
    farmId: [this.contextService.currentFarmValue?.id, [Validators.required]],
    vetName: ['', [Validators.required]],
    visitDate: [new Date().toISOString().substring(0, 10), [Validators.required]],
    visitType: [1, [Validators.required]],
    purpose: [''],
    findings: [''],
    recommendations: [''],
    costBdt: [null as number | null, [Validators.min(0)]],
    nextVisitDate: ['']
  });

  ngOnInit() {
    this.filteredVetNames$ = this.form.get('vetName')!.valueChanges.pipe(
      startWith(''),
      map(value => this._filterVets(value || ''))
    );

    const farmId = this.contextService.currentFarmValue?.id;
    if (farmId) {
      this.healthService.getVetVisits({ farmId, pageSize: 100 }).subscribe(res => {
        const uniqueNames = Array.from(new Set(res.items.map(v => v.vetName).filter(Boolean)));
        this.knownVetNames.set(uniqueNames);
      });
    }
  }

  private _filterVets(value: string): string[] {
    const filterValue = value.toLowerCase();
    return this.knownVetNames().filter(name => name.toLowerCase().includes(filterValue));
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    const command = this.form.getRawValue();

    // Map form values to command structure
    const payload = {
      ...command,
      visitType: Number(command.visitType),
      costBdt: command.costBdt ? Number(command.costBdt) : null,
      nextVisitDate: command.nextVisitDate || null
    };

    this.healthService.createVetVisit(payload).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isSubmitting.set(false))
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        console.error('Failed to log vet visit', err);
        this.error.set(parseApiError(err, 'Failed to log vet visit. Please try again.'));
      }
    });
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}
