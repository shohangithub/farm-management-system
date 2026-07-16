import {
  Component, inject, signal, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule }           from '@angular/common';
import { RouterModule, Router }   from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AnimalService }          from '../../services/animal.service';
import { AnimalSpecies, AnimalSex, AcquisitionType, TagType, SPECIES_LABELS } from '../../models/animal.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';

@Component({
  selector: 'app-animal-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule,
    PageHeaderComponent,
    MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule,
    MatDatepickerModule, MatNativeDateModule
  ],
  template: `
    <div class="max-w-4xl mx-auto">
      <app-page-header 
        title="Register Animal" 
        description="Add a new animal to your herd">
      </app-page-header>

      <form [formGroup]="form" (ngSubmit)="onSubmit()">
        
        <!-- Identification -->
        <mat-card class="mb-6 !bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
          <mat-card-header class="!pb-4 !pt-4 !border-b border-gray-100 dark:border-gray-800">
            <mat-card-title class="!text-lg !font-bold">Identification</mat-card-title>
          </mat-card-header>
          <mat-card-content class="!p-6 grid grid-cols-1 md:grid-cols-2 gap-4">
            
            <mat-form-field appearance="outline" class="w-full md:col-span-2">
              <mat-label>Tag ID</mat-label>
              <input matInput formControlName="tagId" placeholder="e.g. B-001 or 001234">
              <mat-error *ngIf="f['tagId'].errors?.['required']">Tag ID is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Tag Type</mat-label>
              <mat-select formControlName="tagType">
                <mat-option [value]="TagType.Manual">Manual Label</mat-option>
                <mat-option [value]="TagType.EarTag">Ear Tag</mat-option>
                <mat-option [value]="TagType.Rfid">RFID</mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Farm</mat-label>
              <input matInput formControlName="farmId" placeholder="Farm ID (GUID)">
              <mat-error *ngIf="f['farmId'].errors?.['required']">Farm is required</mat-error>
            </mat-form-field>

          </mat-card-content>
        </mat-card>

        <!-- Classification -->
        <mat-card class="mb-6 !bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
          <mat-card-header class="!pb-4 !pt-4 !border-b border-gray-100 dark:border-gray-800">
            <mat-card-title class="!text-lg !font-bold">Classification</mat-card-title>
          </mat-card-header>
          <mat-card-content class="!p-6 grid grid-cols-1 md:grid-cols-2 gap-4">

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Species</mat-label>
              <mat-select formControlName="species">
                <mat-option *ngFor="let s of speciesOptions" [value]="s.value">{{ s.label }}</mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Breed</mat-label>
              <input matInput formControlName="breedName" placeholder="e.g. Holstein-Friesian">
              <mat-error *ngIf="f['breedName'].errors?.['required']">Breed name is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Sex</mat-label>
              <mat-select formControlName="sex">
                <mat-option [value]="AnimalSex.Male">Male</mat-option>
                <mat-option [value]="AnimalSex.Female">Female</mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Date of Birth</mat-label>
              <input matInput type="date" formControlName="dateOfBirth" [max]="today">
              <mat-error *ngIf="f['dateOfBirth'].errors?.['required']">Date of birth is required</mat-error>
            </mat-form-field>

          </mat-card-content>
        </mat-card>

        <!-- Acquisition -->
        <mat-card class="mb-6 !bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
          <mat-card-header class="!pb-4 !pt-4 !border-b border-gray-100 dark:border-gray-800">
            <mat-card-title class="!text-lg !font-bold">Acquisition</mat-card-title>
          </mat-card-header>
          <mat-card-content class="!p-6 grid grid-cols-1 md:grid-cols-2 gap-4">

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Acquisition Type</mat-label>
              <mat-select formControlName="acquisitionType">
                <mat-option [value]="AcquisitionType.Purchased">Purchased</mat-option>
                <mat-option [value]="AcquisitionType.BornOnFarm">Born On Farm</mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Acquisition Date</mat-label>
              <input matInput type="date" formControlName="acquisitionDate" [max]="today">
              <mat-error *ngIf="f['acquisitionDate'].errors?.['required']">Acquisition date is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full" *ngIf="form.get('acquisitionType')?.value == AcquisitionType.Purchased">
              <mat-label>Purchase Price (BDT)</mat-label>
              <input matInput type="number" formControlName="acquisitionPriceBdt" placeholder="0.00" min="0">
            </mat-form-field>

          </mat-card-content>
        </mat-card>

        <!-- Notes -->
        <mat-card class="mb-6 !bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
          <mat-card-header class="!pb-4 !pt-4 !border-b border-gray-100 dark:border-gray-800">
            <mat-card-title class="!text-lg !font-bold">Additional Notes</mat-card-title>
          </mat-card-header>
          <mat-card-content class="!p-6">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Notes</mat-label>
              <textarea matInput formControlName="notes" rows="3" placeholder="Any additional notes about this animal..."></textarea>
            </mat-form-field>
          </mat-card-content>
        </mat-card>

        <!-- Error -->
        <div *ngIf="submitError()" class="p-4 mb-6 text-sm text-red-800 rounded-lg bg-red-50 dark:bg-red-900/20 dark:text-red-400" role="alert">
          {{ submitError() }}
        </div>

        <!-- Actions -->
        <div class="flex gap-4 items-center">
          <button mat-flat-button color="primary" type="submit" [disabled]="submitting() || form.invalid" class="!px-6">
            <span *ngIf="submitting()">Registering…</span>
            <span *ngIf="!submitting()">Register Animal</span>
          </button>
          <button mat-button type="button" routerLink="/livestock">Cancel</button>
        </div>

      </form>
    </div>
  `,
})
export class AnimalRegisterComponent {
  private readonly svc    = inject(AnimalService);
  private readonly router = inject(Router);
  private readonly fb     = inject(FormBuilder);

  readonly submitting  = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly today       = new Date().toISOString().split('T')[0];

  // Expose enums to template
  readonly TagType         = TagType;
  readonly AnimalSex       = AnimalSex;
  readonly AcquisitionType = AcquisitionType;

  readonly speciesOptions = Object.entries(SPECIES_LABELS).map(([v, l]) => ({ value: +v, label: l }));

  readonly form = this.fb.group({
    farmId:             ['', Validators.required],
    tagId:              ['', [Validators.required, Validators.maxLength(50)]],
    tagType:            [TagType.Manual, Validators.required],
    species:            [AnimalSpecies.CattleBeef, Validators.required],
    breedName:          ['', [Validators.required, Validators.maxLength(100)]],
    sex:                [AnimalSex.Male, Validators.required],
    dateOfBirth:        ['', Validators.required],
    acquisitionType:    [AcquisitionType.Purchased, Validators.required],
    acquisitionDate:    ['', Validators.required],
    acquisitionPriceBdt:[null as number | null],
    notes:              [null as string | null],
  });

  get f() { return this.form.controls; }

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true);
    this.submitError.set(null);

    const v = this.form.getRawValue();

    this.svc.register({
      farmId:              v.farmId!,
      tagId:               v.tagId!,
      tagType:             +v.tagType!,
      species:             +v.species!,
      breedName:           v.breedName!,
      sex:                 +v.sex!,
      dateOfBirth:         v.dateOfBirth!,
      acquisitionType:     +v.acquisitionType!,
      acquisitionDate:     v.acquisitionDate!,
      acquisitionPriceBdt: v.acquisitionPriceBdt ?? undefined,
      notes:               v.notes ?? undefined,
    }).subscribe({
      next:  a => this.router.navigate(['/livestock', a.id]),
      error: e => {
        this.submitError.set(e?.error?.detail ?? 'Registration failed. Please try again.');
        this.submitting.set(false);
      }
    });
  }
}
