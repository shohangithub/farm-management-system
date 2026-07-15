import {
  Component, inject, signal, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule }           from '@angular/common';
import { RouterModule, Router }   from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AnimalService }          from '../../services/animal.service';
import { AnimalSpecies, AnimalSex, AcquisitionType, TagType, SPECIES_LABELS } from '../../models/animal.models';

@Component({
  selector: 'app-animal-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, ReactiveFormsModule],
  template: `
    <div style="max-width:720px">

      <nav class="breadcrumb">
        <a routerLink="/">Home</a>
        <span class="separator">›</span>
        <a routerLink="/livestock">Livestock</a>
        <span class="separator">›</span>
        <span>Register Animal</span>
      </nav>

      <div class="page-header">
        <div>
          <h1 class="page-title">Register Animal</h1>
          <p class="page-subtitle">Add a new animal to your herd</p>
        </div>
      </div>

      <form [formGroup]="form" (ngSubmit)="onSubmit()">

        <!-- ── Identification ───────────────────────────── -->
        <div class="card card--elevated" style="margin-bottom:16px">
          <div class="card-header"><h3 style="font-size:1rem">Identification</h3></div>
          <div class="card-body" style="display:grid;grid-template-columns:1fr 1fr;gap:16px">

            <div class="form-group" style="grid-column:1/-1">
              <label class="form-label">Tag ID *</label>
              <input class="form-control" formControlName="tagId" placeholder="e.g. B-001 or 001234" />
              <span class="form-error" *ngIf="f['tagId'].touched && f['tagId'].errors?.['required']">Tag ID is required</span>
            </div>

            <div class="form-group">
              <label class="form-label">Tag Type *</label>
              <select class="form-control" formControlName="tagType">
                <option [value]="TagType.Manual">Manual Label</option>
                <option [value]="TagType.EarTag">Ear Tag</option>
                <option [value]="TagType.Rfid">RFID</option>
              </select>
            </div>

            <div class="form-group">
              <label class="form-label">Farm *</label>
              <input class="form-control" formControlName="farmId" placeholder="Farm ID (GUID)" />
              <span class="form-error" *ngIf="f['farmId'].touched && f['farmId'].errors?.['required']">Farm is required</span>
            </div>

          </div>
        </div>

        <!-- ── Classification ──────────────────────────── -->
        <div class="card card--elevated" style="margin-bottom:16px">
          <div class="card-header"><h3 style="font-size:1rem">Classification</h3></div>
          <div class="card-body" style="display:grid;grid-template-columns:1fr 1fr;gap:16px">

            <div class="form-group">
              <label class="form-label">Species *</label>
              <select class="form-control" formControlName="species">
                <option *ngFor="let s of speciesOptions" [value]="s.value">{{ s.label }}</option>
              </select>
            </div>

            <div class="form-group">
              <label class="form-label">Breed *</label>
              <input class="form-control" formControlName="breedName" placeholder="e.g. Shahibal, Holstein-Friesian" />
              <span class="form-error" *ngIf="f['breedName'].touched && f['breedName'].errors?.['required']">Breed name is required</span>
            </div>

            <div class="form-group">
              <label class="form-label">Sex *</label>
              <select class="form-control" formControlName="sex">
                <option [value]="AnimalSex.Male">Male ♂</option>
                <option [value]="AnimalSex.Female">Female ♀</option>
              </select>
            </div>

            <div class="form-group">
              <label class="form-label">Date of Birth *</label>
              <input class="form-control" type="date" formControlName="dateOfBirth" [max]="today" />
              <span class="form-error" *ngIf="f['dateOfBirth'].touched && f['dateOfBirth'].errors?.['required']">Date of birth is required</span>
            </div>

          </div>
        </div>

        <!-- ── Acquisition ─────────────────────────────── -->
        <div class="card card--elevated" style="margin-bottom:16px">
          <div class="card-header"><h3 style="font-size:1rem">Acquisition</h3></div>
          <div class="card-body" style="display:grid;grid-template-columns:1fr 1fr;gap:16px">

            <div class="form-group">
              <label class="form-label">Acquisition Type *</label>
              <select class="form-control" formControlName="acquisitionType">
                <option [value]="AcquisitionType.Purchased">Purchased</option>
                <option [value]="AcquisitionType.BornOnFarm">Born On Farm</option>
              </select>
            </div>

            <div class="form-group">
              <label class="form-label">Acquisition Date *</label>
              <input class="form-control" type="date" formControlName="acquisitionDate" [max]="today" />
            </div>

            <div class="form-group" *ngIf="form.get('acquisitionType')?.value == AcquisitionType.Purchased">
              <label class="form-label">Purchase Price (BDT)</label>
              <input class="form-control" type="number" formControlName="acquisitionPriceBdt" placeholder="0.00" min="0" />
            </div>

          </div>
        </div>

        <!-- ── Notes ──────────────────────────────────── -->
        <div class="card card--elevated" style="margin-bottom:24px">
          <div class="card-header"><h3 style="font-size:1rem">Additional Notes</h3></div>
          <div class="card-body">
            <textarea class="form-control" formControlName="notes"
              placeholder="Any additional notes about this animal..." rows="3"></textarea>
          </div>
        </div>

        <!-- ── Error ──────────────────────────────────── -->
        <div *ngIf="submitError()" style="margin-bottom:16px;padding:12px 16px;background:rgba(239,68,68,0.1);border:1px solid rgba(239,68,68,0.3);border-radius:8px;color:var(--color-danger);font-size:0.875rem">
          {{ submitError() }}
        </div>

        <!-- ── Actions ────────────────────────────────── -->
        <div class="d-flex gap-3 align-center">
          <button type="submit" class="btn btn-primary" [disabled]="submitting() || form.invalid">
            <span *ngIf="submitting()">Registering…</span>
            <span *ngIf="!submitting()">Register Animal</span>
          </button>
          <a routerLink="/livestock" class="btn btn-secondary">Cancel</a>
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
