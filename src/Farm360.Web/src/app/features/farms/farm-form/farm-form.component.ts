import { Component, OnInit, inject, signal, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { FarmService } from '../services/farm.service';
import { CreateFarmCommand, UpdateFarmCommand } from '../models/farm.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, merge } from 'rxjs';
import { FarmMapComponent, PickedLocation } from '../../../shared/components/farm-map/farm-map.component';
import { NotificationService } from '../../../shared/services/notification.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

import { HasUnsavedChanges } from '../../../shared/utils/unsaved-changes.guard';

@Component({
  selector: 'app-farm-form',
  standalone: true,
  imports: [TranslatePipe, CommonModule, ReactiveFormsModule, RouterModule, PageHeaderComponent, MatSnackBarModule, FarmMapComponent],
  templateUrl: './farm-form.component.html'
})
export class FarmFormComponent implements OnInit, HasUnsavedChanges {
  private readonly i18n = inject(I18nService);
  /** Set once the form is saved so the unsaved-changes guard lets navigation through. */
  private saved = false;

  hasUnsavedChanges(): boolean {
    return !!this.farmForm?.dirty && !this.saved;
  }

  private readonly fb = inject(FormBuilder);
  private readonly farmService = inject(FarmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);

  farmForm!: FormGroup;
  isEditMode = signal<boolean>(false);
  branchId = signal<string>('');
  farmId = signal<string | null>(null);
  isSubmitting = signal<boolean>(false);
  error = signal<string | null>(null);

  /** Reading the browser's location for the farm coordinates. */
  locating = signal<boolean>(false);
  locationError = signal<string | null>(null);

  /** The point shown on the location map: follows the Latitude/Longitude fields (typing is debounced). */
  mapLat = signal<number | null>(null);
  mapLon = signal<number | null>(null);
  mapLabel = signal<string>('');

  /** A click or drag on the map fills the fields and marks the form dirty (the unsaved-changes guard sees it). */
  onLocationPicked(point: PickedLocation): void {
    this.locationError.set(null);
    this.setCoordinates(point.lat, point.lon);
  }

  private setCoordinates(lat: number, lon: number): void {
    this.farmForm.patchValue({ latitude: lat, longitude: lon });
    for (const name of ['latitude', 'longitude']) {
      this.farmForm.get(name)?.markAsDirty();
      this.farmForm.get(name)?.markAsTouched();
    }
    this.farmForm.markAsDirty();
    this.mapLat.set(lat);
    this.mapLon.set(lon);
  }

  /** Bangladesh, roughly (with a margin): coordinates outside get a warning, not an error. */
  private static readonly BD_BOUNDS = { minLat: 20.5, maxLat: 26.8, minLon: 88.0, maxLon: 92.8 };

  /** Fill latitude/longitude from the device's GPS / browser location (stand at the farm). */
  useCurrentLocation(): void {
    this.locationError.set(null);
    if (typeof navigator === 'undefined' || !navigator.geolocation) {
      this.locationError.set(this.i18n.t('This browser cannot share its location. Copy the point from Google Maps instead.'));
      return;
    }
    this.locating.set(true);
    navigator.geolocation.getCurrentPosition(
      position => {
        this.locating.set(false);
        this.setCoordinates(
          Math.round(position.coords.latitude * 1e6) / 1e6,
          Math.round(position.coords.longitude * 1e6) / 1e6);
      },
      err => {
        this.locating.set(false);
        this.locationError.set(this.i18n.t(err.code === err.PERMISSION_DENIED
          ? 'Location permission was refused. Allow it in the browser, or copy the point from Google Maps.'
          : 'Could not read your location. Try again outdoors, or copy the point from Google Maps.'));
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 60000 }
    );
  }

  /** Both coordinates are set and valid but the point is outside Bangladesh (probably swapped or mistyped). */
  get outsideBangladesh(): boolean {
    const lat = Number(this.farmForm?.get('latitude')?.value);
    const lon = Number(this.farmForm?.get('longitude')?.value);
    const latRaw = this.farmForm?.get('latitude')?.value;
    const lonRaw = this.farmForm?.get('longitude')?.value;
    if (latRaw === null || latRaw === '' || lonRaw === null || lonRaw === '' || !Number.isFinite(lat) || !Number.isFinite(lon)) return false;
    if (this.farmForm.get('latitude')?.invalid || this.farmForm.get('longitude')?.invalid) return false;
    const b = FarmFormComponent.BD_BOUNDS;
    return lat < b.minLat || lat > b.maxLat || lon < b.minLon || lon > b.maxLon;
  }

  ngOnInit(): void {
    this.initForm();

    const branchId = this.route.snapshot.paramMap.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId');
    const farmId = this.route.snapshot.paramMap.get('farmId');

    if (branchId) {
      this.branchId.set(branchId);
    }

    if (farmId) {
      this.isEditMode.set(true);
      this.farmId.set(farmId);
      this.loadFarm(farmId);
    }

    // Typed coordinates move the map pin (debounced so a half-typed number does not jump the map around).
    const latControl = this.farmForm.get('latitude')!;
    const lonControl = this.farmForm.get('longitude')!;
    merge(latControl.valueChanges, lonControl.valueChanges)
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const toNumber = (v: unknown) => v === null || v === '' || v === undefined || !Number.isFinite(Number(v)) ? null : Number(v);
        this.mapLat.set(latControl.valid ? toNumber(latControl.value) : null);
        this.mapLon.set(lonControl.valid ? toNumber(lonControl.value) : null);
      });
    this.farmForm.get('farmName')!.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(name => this.mapLabel.set(name ?? ''));

    // Auto-uppercase farm code as the user types
    this.farmForm.get('farmCode')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(value => {
        if (value && value !== value.toUpperCase()) {
          this.farmForm.get('farmCode')?.setValue(value.toUpperCase(), { emitEvent: false });
        }
      });
  }

  initForm(): void {
    this.farmForm = this.fb.group({
      farmCode: ['', [Validators.required, Validators.maxLength(50)]],
      farmName: ['', [Validators.required, Validators.maxLength(200)]],
      type: ['Dairy', Validators.required],
      farmSize: [null, Validators.min(0)],
      landArea: [null, Validators.min(0)],
      latitude: [null, [Validators.min(-90), Validators.max(90)]],
      longitude: [null, [Validators.min(-180), Validators.max(180)]],
      mapPolygon: [''],
      capacity: [null, Validators.min(0)],
      status: ['Active'],
      description: ['', Validators.maxLength(1000)]
    });
  }

  loadFarm(id: string): void {
    this.farmService.getFarmById(id).subscribe({
      next: (farm) => {
        this.farmForm.patchValue({
          farmCode: farm.farmCode,
          farmName: farm.farmName,
          type: farm.type,
          farmSize: farm.farmSize,
          landArea: farm.landArea,
          latitude: farm.latitude,
          longitude: farm.longitude,
          mapPolygon: farm.mapPolygon,
          capacity: farm.capacity,
          status: farm.status,
          description: farm.description
        });
        // Farm code shouldn't change after creation
        this.farmForm.get('farmCode')?.disable();
      },
      error: (err) => {
        this.error.set(this.i18n.t('Failed to load farm details.'));
        console.error(err);
      }
    });
  }

  getError(field: string): string {
    const control = this.farmForm.get(field);
    if (!control || !control.touched || control.valid) return '';
    if (control.hasError('required')) return 'This field is required.';
    if (control.hasError('maxlength')) {
      const max = control.getError('maxlength').requiredLength;
      return `Maximum ${max} characters allowed.`;
    }
    if (control.hasError('min')) {
      return this.i18n.t('Value must be at least {min}.', { min: control.getError('min').min });
    }
    if (control.hasError('max')) {
      return this.i18n.t('Value must be at most {max}.', { max: control.getError('max').max });
    }
    return 'Invalid value.';
  }

  onSubmit(): void {
    if (this.farmForm.invalid) {
      this.farmForm.markAllAsTouched();
      this.notify.error(this.i18n.t('Please fix the validation errors before submitting.'));
      return;
    }

    this.isSubmitting.set(true);
    this.error.set(null);

    const formValue = this.farmForm.getRawValue();

    // Sanitize empty strings to null to prevent BadHttpRequestException during deserialization
    Object.keys(formValue).forEach(key => {
      if (formValue[key] === '') {
        formValue[key] = null;
      }
    });

    if (this.isEditMode() && this.farmId()) {
      const command: UpdateFarmCommand = {
        id: this.farmId()!,
        branchId: this.branchId(),
        ...formValue
      };
      this.farmService.updateFarm(this.farmId()!, command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.notify.success(this.i18n.t('Farm updated successfully!'));
          this.saved = true;
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || this.i18n.t('Failed to update farm. Please check the inputs.');
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    } else {
      const command: CreateFarmCommand = {
        branchId: this.branchId(),
        ...formValue
      };
      this.farmService.createFarm(this.branchId(), command).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.notify.success(this.i18n.t('Farm created successfully!'));
          this.saved = true;
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
        },
        error: (err) => {
          const message = err?.error?.detail || err?.error?.title || this.i18n.t('Failed to create farm. Please check the inputs.');
          this.error.set(message);
          this.isSubmitting.set(false);
          console.error(err);
        }
      });
    }
  }
}
