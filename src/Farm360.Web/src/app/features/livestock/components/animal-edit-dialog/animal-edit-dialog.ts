import {
  Component, inject, signal, ChangeDetectionStrategy, OnInit, Inject, DestroyRef
} from '@angular/core';
import { CommonModule }           from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { AnimalService }          from '../../services/animal.service';
import { BreedService }           from '../../services/breed.service';
import { AnimalDto, AnimalSpecies, AnimalSex, AcquisitionType, TagType, SPECIES_LABELS } from '../../models/animal.models';
import { BreedDto }               from '../../models/breed.models';
import { NotificationService } from '../../../../shared/services/notification.service';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-animal-edit-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule, TranslatePipe
  ],
  templateUrl: './animal-edit-dialog.html'
})
export class AnimalEditDialogComponent implements OnInit {
  private readonly svc      = inject(AnimalService);
  private readonly fb       = inject(FormBuilder);
  private readonly notify = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly breedSvc = inject(BreedService);
  private readonly i18n = inject(I18nService);
  
  readonly submitting  = signal(false);
  readonly error       = signal<string | null>(null);
  readonly breeds      = signal<BreedDto[]>([]);

  form!: FormGroup;

  readonly TagType         = TagType;
  readonly AnimalSex       = AnimalSex;
  readonly AcquisitionType = AcquisitionType;
  readonly speciesOptions = Object.entries(SPECIES_LABELS).map(([v, l]) => ({ value: v, label: l }));

  constructor(
    public dialogRef: MatDialogRef<AnimalEditDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public animal: AnimalDto
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      tagId:              [this.animal.tagId, [Validators.required, Validators.maxLength(50)]],
      tagType:            [this.animal.tagType, Validators.required],
      species:            [this.animal.species, Validators.required],
      breedId:            [this.animal.breedId, [Validators.required]],
      sex:                [this.animal.sex, Validators.required],
      dateOfBirth:        [this.animal.dateOfBirth, Validators.required],
      acquisitionType:    [this.animal.acquisitionType, Validators.required],
      acquisitionDate:    [this.animal.acquisitionDate, Validators.required],
      acquisitionPriceBdt:[this.animal.acquisitionPriceBdt, Validators.min(0)],
      notes:              [this.animal.notes, Validators.maxLength(2000)],
    });

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
    if (control.hasError('required')) return this.i18n.t('Required.');
    if (control.hasError('maxlength')) return this.i18n.t('Too long.');
    if (control.hasError('min')) return this.i18n.t('Must be >= 0.');
    return this.i18n.t('Invalid.');
  }

  onSubmit(): void {
    if (this.form.invalid) { 
      this.form.markAllAsTouched(); 
      return; 
    }
    
    this.submitting.set(true);
    this.error.set(null);

    const vals = this.form.getRawValue();
    
    this.svc.update(this.animal.id, {
      id:                  this.animal.id,
      tagId:               vals.tagId!,
      tagType:             vals.tagType as TagType,
      species:             vals.species as AnimalSpecies,
      breedId:             vals.breedId!,
      sex:                 vals.sex as AnimalSex,
      dateOfBirth:         vals.dateOfBirth!,
      acquisitionType:     vals.acquisitionType as AcquisitionType,
      acquisitionDate:     vals.acquisitionDate!,
      acquisitionPriceBdt: vals.acquisitionPriceBdt === '' ? undefined : vals.acquisitionPriceBdt,
      notes:               vals.notes === '' ? undefined : vals.notes,
    })
    .pipe(finalize(() => this.submitting.set(false)))
    .subscribe({
      next:  a => {
        this.notify.success(this.i18n.t('Animal details updated successfully!'));
        this.dialogRef.close(true);
      },
      error: err => {
        let msg = this.i18n.t('Failed to update animal.');
        if (err.error?.title) msg = err.error.title;
        if (err.error?.detail) msg += ` - ${err.error.detail}`;
        
        if (err.status === 422 && err.error?.errors) {
          const firstErrorKey = Object.keys(err.error.errors)[0];
          if (firstErrorKey) {
            msg = err.error.errors[firstErrorKey][0];
          }
        }

        this.error.set(msg);
      }
    });
  }

  /** Human-readable message for the first validation error on a control. */
  fieldError(group: { get(name: string): import('@angular/forms').AbstractControl | null }, name: string): string {
    const e = group.get(name)?.errors;
    if (!e) return '';
    if (e['required']) return this.i18n.t('This field is required.');
    if (e['min']) return this.i18n.t('Must be at least {min}.', { min: e['min'].min });
    if (e['max']) return this.i18n.t('Must be at most {max}.', { max: e['max'].max });
    if (e['maxlength']) return this.i18n.t('Must be {n} characters or fewer.', { n: e['maxlength'].requiredLength });
    if (e['minlength']) return this.i18n.t('Must be at least {n} characters.', { n: e['minlength'].requiredLength });
    if (e['email']) return this.i18n.t('Enter a valid email address.');
    if (e['pattern']) return this.i18n.t('Invalid format.');
    return this.i18n.t('Invalid value.');
  }
}
