import {
  Component, inject, signal, ChangeDetectionStrategy, OnInit
} from '@angular/core';
import { CommonModule }           from '@angular/common';
import { RouterModule, Router }   from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { AnimalService }          from '../../services/animal.service';
import { WorkingContextService }  from '../../../../core/services/working-context.service';
import { AnimalSpecies, AnimalSex, AcquisitionType, TagType, SPECIES_LABELS } from '../../models/animal.models';
import { MatSnackBarModule } from '@angular/material/snack-bar';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef } from '@angular/core';
import { BreedService } from '../../services/breed.service';
import { BreedDto } from '../../models/breed.models';
import { ShedService } from '../../../farms/services/shed.service';
import { PenService } from '../../../farms/services/pen.service';

import { MatStepperModule } from '@angular/material/stepper';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { NotificationService } from '../../../../shared/services/notification.service';
import { HasUnsavedChanges } from '../../../../shared/utils/unsaved-changes.guard';
import { parseApiError } from '../../../../core/utils/error-parser';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { HealthService } from '../../../health/services/health.service';
import { AutoStartProgrammeDto } from '../../../health/models/health.models';
import { AuthService } from '../../../../core/services/auth.service';
import { combineLatest, startWith, switchMap, of, catchError } from 'rxjs';

@Component({
  selector: 'app-animal-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule,
    PageHeaderComponent, MatSnackBarModule, MatStepperModule, MatButtonModule, MatIconModule, TranslatePipe
  ],
  templateUrl: './animal-register.component.html'
})
export class AnimalRegisterComponent implements OnInit, HasUnsavedChanges {
  private saved = false;

  hasUnsavedChanges(): boolean {
    return !this.saved && [this.idForm, this.acquisitionForm, this.notesForm, this.placementForm].some(f => f?.dirty);
  }

  private readonly svc      = inject(AnimalService);
  private readonly contextSvc = inject(WorkingContextService);
  private readonly router   = inject(Router);
  private readonly fb       = inject(FormBuilder);
  private readonly notify = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly breedSvc = inject(BreedService);
  private readonly i18n = inject(I18nService);
  readonly addAnimalDescription = this.i18n.t("Add a new animal to your organization's herd.");
  
  readonly submitting  = signal(false);
  readonly error       = signal<string | null>(null);
  readonly today       = toLocalIsoDate();
  readonly breeds      = signal<BreedDto[]>([]);

  private readonly healthSvc = inject(HealthService);
  private readonly auth = inject(AuthService);
  /** Health programmes that start by themselves for this purchased animal (step 4 summary). */
  readonly autoProgrammes = signal<AutoStartProgrammeDto[]>([]);
  /** The user unticked "start the programme" for this animal. */
  readonly skipHealthProgramme = signal(false);
  
  private readonly shedSvc = inject(ShedService);
  private readonly penSvc = inject(PenService);
  readonly sheds = signal<any[]>([]);
  readonly pens = signal<any[]>([]);

  idForm!: FormGroup;
  acquisitionForm!: FormGroup;
  notesForm!: FormGroup;
  placementForm!: FormGroup;

  // Expose enums to template
  readonly TagType         = TagType;
  readonly AnimalSex       = AnimalSex;
  readonly AcquisitionType = AcquisitionType;

  readonly speciesOptions = Object.entries(SPECIES_LABELS).map(([v, l]) => ({ value: v, label: l }));

  constructor() {}

  ngOnInit(): void {
    this.idForm = this.fb.group({
      tagId:              ['', [Validators.required, Validators.maxLength(50)]],
      tagType:            [TagType.Manual, Validators.required],
      species:            [AnimalSpecies.CattleBeef, Validators.required],
      breedId:            ['', [Validators.required]],
      sex:                [AnimalSex.Male, Validators.required],
    });

    this.acquisitionForm = this.fb.group({
      dateOfBirth:        ['', Validators.required],
      acquisitionType:    [AcquisitionType.Purchased, Validators.required],
      acquisitionDate:    ['', Validators.required],
      acquisitionPriceBdt:[null as number | null, Validators.min(0)],
    });

    this.notesForm = this.fb.group({
      notes:              [null as string | null, Validators.maxLength(1000)],
    });

    this.placementForm = this.fb.group({
      shedId:             [null as string | null],
      penId:              [null as string | null],
    });

    // Step 4: which automatic health programmes will start (purchased animals only).
    combineLatest([
      this.idForm.get('species')!.valueChanges.pipe(startWith(this.idForm.get('species')!.value)),
      this.acquisitionForm.get('acquisitionType')!.valueChanges.pipe(startWith(this.acquisitionForm.get('acquisitionType')!.value)),
    ]).pipe(
      switchMap(([species, type]) => type === AcquisitionType.Purchased && species && this.auth.hasAnyPermission(['health.view'])
        ? this.healthSvc.getAutoStartProgrammes(species).pipe(catchError(() => of([] as AutoStartProgrammeDto[])))
        : of([] as AutoStartProgrammeDto[])),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(list => this.autoProgrammes.set(list));

    // Auto-uppercase tagId
    this.idForm.get('tagId')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(value => {
        if (value && value !== value.toUpperCase()) {
          this.idForm.get('tagId')?.setValue(value.toUpperCase(), { emitEvent: false });
        }
      });

    this.breedSvc.getBreeds({ pageSize: 1000 }).subscribe({
      next: (b) => this.breeds.set(b.items)
    });

    // Load Sheds
    this.contextSvc.currentFarm$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(farm => {
      if (farm) {
        this.shedSvc.getShedsByFarm(farm.id).subscribe({
          next: (s) => this.sheds.set(s)
        });
      } else {
        this.sheds.set([]);
      }
      this.placementForm.patchValue({ shedId: null, penId: null });
    });

    // Load Pens when Shed changes
    this.placementForm.get('shedId')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(shedId => {
        this.placementForm.get('penId')?.setValue(null, { emitEvent: false });
        if (shedId) {
          this.penSvc.getPensByShed(shedId).subscribe({
            next: (p) => this.pens.set(p)
          });
        } else {
          this.pens.set([]);
        }
      });
  }

  getError(formGroup: FormGroup, field: string): string {
    const control = formGroup.get(field);
    if (!control || !control.touched || control.valid) return '';
    if (control.hasError('required')) return this.i18n.t('This field is required.');
    if (control.hasError('maxlength')) {
      const max = control.getError('maxlength').requiredLength;
      return this.i18n.t('Maximum {max} characters allowed.', { max });
    }
    if (control.hasError('min')) {
      return this.i18n.t('Value must be zero or positive.');
    }
    return this.i18n.t('Invalid value.');
  }

  onSubmit(): void {
    if (this.idForm.invalid || this.acquisitionForm.invalid || this.notesForm.invalid) { 
      this.idForm.markAllAsTouched(); 
      this.acquisitionForm.markAllAsTouched();
      this.notesForm.markAllAsTouched();
      this.notify.error(this.i18n.t('Please fix the validation errors before submitting.'));
      return; 
    }
    
    this.submitting.set(true);
    this.error.set(null);

    const idVals = this.idForm.getRawValue();
    const acqVals = this.acquisitionForm.getRawValue();
    const notesVals = this.notesForm.getRawValue();
    const placementVals = this.placementForm.getRawValue();
    const farmId = this.contextSvc.currentFarmValue?.id;
    
    if (!farmId) {
      this.notify.error(this.i18n.t('Please select a farm context from the top navigation before registering an animal.'));
      return;
    }

    this.svc.register({
      farmId:              farmId,
      tagId:               idVals.tagId!,
      tagType:             idVals.tagType as TagType,
      species:             idVals.species as AnimalSpecies,
      breedId:             idVals.breedId!,
      sex:                 idVals.sex as AnimalSex,
      dateOfBirth:         acqVals.dateOfBirth!,
      acquisitionType:     acqVals.acquisitionType as AcquisitionType,
      acquisitionDate:     acqVals.acquisitionDate!,
      acquisitionPriceBdt: acqVals.acquisitionPriceBdt ?? undefined,
      notes:               notesVals.notes ?? undefined,
      skipHealthProgramme: this.autoProgrammes().length > 0 && this.skipHealthProgramme(),
      shedId:              placementVals.shedId ?? undefined,
      penId:               placementVals.penId ?? undefined,
    }).subscribe({
      next:  a => {
        this.saved = true;
        this.notify.success(this.i18n.t('Animal registered successfully!'));
        this.router.navigate(['/livestock', a.id]);
      },
      error: e => {
        this.error.set(parseApiError(e, this.i18n.t('Registration failed. Please try again.')));
        this.submitting.set(false);
      }
    });
  }
}
