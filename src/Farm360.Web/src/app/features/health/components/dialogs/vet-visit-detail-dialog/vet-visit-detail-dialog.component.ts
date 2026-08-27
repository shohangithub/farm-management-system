import { ChangeDetectionStrategy, Component, DestroyRef, Inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { HealthService } from '../../../services/health.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { VetVisitDto } from '../../../models/health.models';
import { finalize, map, startWith } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-vet-visit-detail-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, MatAutocompleteModule, MatInputModule],
  templateUrl: './vet-visit-detail-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VetVisitDetailDialogComponent implements OnInit {
  isEditing = signal(false);
  isLoading = signal(true);
  isSaving = signal(false);
  
  visitData = signal<VetVisitDto | null>(null);
  
  knownVetNames = signal<string[]>([]);
  filteredVetNames$!: Observable<string[]>;

  form = this.fb.group({
    vetName: ['', [Validators.required]],
    visitDate: ['', [Validators.required]],
    visitType: [1, [Validators.required]],
    purpose: [''],
    findings: [''],
    recommendations: [''],
    costBdt: [0, [Validators.min(0)]],
    nextVisitDate: ['']
  });

  constructor(
    private dialogRef: MatDialogRef<VetVisitDetailDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { visitId: string },
    private fb: FormBuilder,
    private healthService: HealthService,
    private destroyRef: DestroyRef
  ) {}

  ngOnInit(): void {
    this.filteredVetNames$ = this.form.get('vetName')!.valueChanges.pipe(
      startWith(''),
      map(value => this._filterVets(value || ''))
    );
    this.loadVisitData();
  }

  private _filterVets(value: string): string[] {
    const filterValue = value.toLowerCase();
    return this.knownVetNames().filter(name => name.toLowerCase().includes(filterValue));
  }

  loadVisitData(): void {
    this.isLoading.set(true);
    this.healthService.getVetVisitDetail(this.data.visitId)
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (visit) => {
          this.visitData.set(visit);
          this.form.patchValue({
            vetName: visit.vetName,
            visitDate: visit.visitDate,
            visitType: visit.visitTypeId || 1,
            purpose: visit.purpose || '',
            findings: visit.findings || '',
            recommendations: visit.recommendations || '',
            costBdt: visit.costBdt || 0,
            nextVisitDate: visit.nextVisitDate || ''
          });
          
          if (visit.farmId) {
            this.healthService.getVetVisits({ farmId: visit.farmId, pageSize: 100 }).subscribe(res => {
              const uniqueNames = Array.from(new Set(res.items.map(v => v.vetName).filter(Boolean)));
              this.knownVetNames.set(uniqueNames);
            });
          }
        },
        error: (err) => {
          console.error('Failed to load visit details', err);
          this.dialogRef.close();
        }
      });
  }

  toggleEdit(): void {
    this.isEditing.set(!this.isEditing());
    if (!this.isEditing() && this.visitData()) {
      // Revert changes if cancel editing
      const visit = this.visitData()!;
      this.form.patchValue({
        vetName: visit.vetName,
        visitDate: visit.visitDate,
        visitType: visit.visitTypeId || 1,
        purpose: visit.purpose || '',
        findings: visit.findings || '',
        recommendations: visit.recommendations || '',
        costBdt: visit.costBdt || 0,
        nextVisitDate: visit.nextVisitDate || ''
      });
    }
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    const payload = this.form.value;

    this.healthService.updateVetVisit(this.data.visitId, payload)
      .pipe(
        finalize(() => this.isSaving.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: () => {
          this.dialogRef.close(true); // Return true to indicate success
        },
        error: (err) => {
          console.error('Failed to update visit', err);
        }
      });
  }
}
