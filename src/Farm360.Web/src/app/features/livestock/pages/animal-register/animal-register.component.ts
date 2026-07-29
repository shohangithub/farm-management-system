import {
  Component, inject, signal, ChangeDetectionStrategy, OnInit
} from '@angular/core';
import { CommonModule }           from '@angular/common';
import { RouterModule, Router }   from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { AnimalService }          from '../../services/animal.service';
import { WorkingContextService }  from '../../../../core/services/working-context.service';
import { AnimalSpecies, AnimalSex, AcquisitionType, TagType, SPECIES_LABELS } from '../../models/animal.models';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef } from '@angular/core';
import { BreedService } from '../../services/breed.service';
import { BreedDto } from '../../models/breed.models';

@Component({
  selector: 'app-animal-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule,
    PageHeaderComponent, MatSnackBarModule
  ],
  templateUrl: './animal-register.component.html'
})
export class AnimalRegisterComponent implements OnInit {
  private readonly svc      = inject(AnimalService);
  private readonly contextSvc = inject(WorkingContextService);
  private readonly router   = inject(Router);
  private readonly fb       = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly breedSvc = inject(BreedService);
  
  readonly submitting  = signal(false);
  readonly error       = signal<string | null>(null);
  readonly today       = new Date().toISOString().split('T')[0];
  readonly breeds      = signal<BreedDto[]>([]);

  form!: FormGroup;

  // Expose enums to template
  readonly TagType         = TagType;
  readonly AnimalSex       = AnimalSex;
  readonly AcquisitionType = AcquisitionType;

  readonly speciesOptions = Object.entries(SPECIES_LABELS).map(([v, l]) => ({ value: +v, label: l }));

  constructor() {}

  ngOnInit(): void {
    this.form = this.fb.group({
      tagId:              ['', [Validators.required, Validators.maxLength(50)]],
      tagType:            [TagType.Manual, Validators.required],
      species:            [AnimalSpecies.CattleBeef, Validators.required],
      breedId:            ['', [Validators.required]],
      sex:                [AnimalSex.Male, Validators.required],
      dateOfBirth:        ['', Validators.required],
      acquisitionType:    [AcquisitionType.Purchased, Validators.required],
      acquisitionDate:    ['', Validators.required],
      acquisitionPriceBdt:[null as number | null, Validators.min(0)],
      notes:              [null as string | null, Validators.maxLength(1000)],
    });

    // Auto-uppercase tagId
    this.form.get('tagId')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(value => {
        if (value && value !== value.toUpperCase()) {
          this.form.get('tagId')?.setValue(value.toUpperCase(), { emitEvent: false });
        }
      });

    this.breedSvc.getBreeds({ pageSize: 1000 }).subscribe({
      next: (b) => this.breeds.set(b.items)
    });
  }

  getError(field: string): string {
    const control = this.form.get(field);
    if (!control || !control.touched || control.valid) return '';
    if (control.hasError('required')) return 'This field is required.';
    if (control.hasError('maxlength')) {
      const max = control.getError('maxlength').requiredLength;
      return `Maximum ${max} characters allowed.`;
    }
    if (control.hasError('min')) {
      return 'Value must be zero or positive.';
    }
    return 'Invalid value.';
  }

  onSubmit(): void {
    if (this.form.invalid) { 
      this.form.markAllAsTouched(); 
      this.snackBar.open('Please fix the validation errors before submitting.', 'OK', {
        duration: 4000,
        panelClass: ['snack-error']
      });
      return; 
    }
    
    this.submitting.set(true);
    this.error.set(null);

    const v = this.form.getRawValue();
    const farmId = this.contextSvc.currentFarmValue?.id;
    
    if (!farmId) {
      this.snackBar.open('Please select a farm context from the top navigation before registering an animal.', 'OK', {
        duration: 4000,
        panelClass: ['snack-error']
      });
      return;
    }

    this.svc.register({
      farmId:              farmId,
      tagId:               v.tagId!,
      tagType:             +v.tagType!,
      species:             +v.species!,
      breedId:             v.breedId!,
      sex:                 +v.sex!,
      dateOfBirth:         v.dateOfBirth!,
      acquisitionType:     +v.acquisitionType!,
      acquisitionDate:     v.acquisitionDate!,
      acquisitionPriceBdt: v.acquisitionPriceBdt ?? undefined,
      notes:               v.notes ?? undefined,
    }).subscribe({
      next:  a => {
        this.snackBar.open('Animal registered successfully!', 'Close', {
          duration: 3000,
          panelClass: ['snack-success']
        });
        this.router.navigate(['/livestock', a.id]);
      },
      error: e => {
        this.error.set(e?.error?.detail ?? e?.error?.title ?? 'Registration failed. Please try again.');
        this.submitting.set(false);
      }
    });
  }
}
