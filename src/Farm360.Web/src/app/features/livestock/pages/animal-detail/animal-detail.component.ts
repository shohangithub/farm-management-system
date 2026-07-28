import {
  Component, inject, signal, ChangeDetectionStrategy, computed,
  ViewChild
} from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { AnimalService }       from '../../services/animal.service';
import { ShedService }       from '../../../farms/services/shed.service';
import { PenService }        from '../../../farms/services/pen.service';
import { HealthService }     from '../../../health/services/health.service';
import { ShedList }          from '../../../farms/models/shed.model';
import { PenList }           from '../../../farms/models/pen.model';
import { AnimalHealthHistoryDto, VaccinationStatus, TreatmentStatus } from '../../../health/models/health.models';
import {
  AnimalDto, AnimalStatus, AnimalSex,
  SPECIES_LABELS, STATUS_LABELS, SEX_LABELS,
} from '../../models/animal.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { FarmService } from '../../../farms/services/farm.service';
import { BatchService } from '../../services/batch.service';
import { BatchDto } from '../../models/batch.models';
import { TransferAnimalDialogComponent } from '../../dialogs/transfer-animal-dialog/transfer-animal-dialog.component';
import { QuarantineDialogComponent } from '../../dialogs/quarantine-dialog/quarantine-dialog.component';
import { MatingDialogComponent } from '../../dialogs/mating-dialog/mating-dialog.component';
import { ConfirmPregnancyDialogComponent } from '../../dialogs/confirm-pregnancy-dialog/confirm-pregnancy-dialog.component';
import { RecordCalvingDialogComponent } from '../../dialogs/record-calving-dialog/record-calving-dialog.component';
import { AssignBatchDialogComponent } from '../../dialogs/assign-batch-dialog/assign-batch-dialog.component';
import { RecordBcsDialogComponent } from '../../dialogs/record-bcs-dialog/record-bcs-dialog.component';
import { RecordWeightDialogComponent } from '../../dialogs/record-weight-dialog/record-weight-dialog.component';
import { UploadPhotoDialogComponent } from '../../dialogs/upload-photo-dialog/upload-photo-dialog.component';
import { RecordSaleDialogComponent } from '../../dialogs/record-sale-dialog/record-sale-dialog.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, forkJoin, tap, filter } from 'rxjs';
import { of } from 'rxjs';

@Component({
  selector: 'app-animal-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterModule, FormsModule,
    PageHeaderComponent, DatePipe, DecimalPipe,
    MatButtonModule, MatIconModule, MatDialogModule, MatTabsModule, MatSnackBarModule, MatMenuModule, MatDividerModule
  ],
  templateUrl: './animal-detail.component.html'
})
export class AnimalDetailComponent {
  private readonly svc     = inject(AnimalService);
  private readonly route   = inject(ActivatedRoute);
  private readonly shedSvc = inject(ShedService);
  private readonly penSvc  = inject(PenService);
  private readonly farmSvc = inject(FarmService);
  private readonly healthSvc = inject(HealthService);
  private readonly batchSvc = inject(BatchService);
  private readonly dialog  = inject(MatDialog);
  private readonly router  = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  readonly AnimalStatus = AnimalStatus;
  readonly AnimalSex = AnimalSex;
  readonly VaccinationStatus = VaccinationStatus;
  readonly TreatmentStatus = TreatmentStatus;

  readonly Math = Math;

  private refreshTrigger = signal(0);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  private routeId = toSignal(this.route.paramMap.pipe(map(params => params.get('id'))), { initialValue: null });

  private fetchParams = computed(() => ({
    id: this.routeId(),
    refresh: this.refreshTrigger()
  }));

  private animalDataResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.id),
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(({ id }) => 
        this.svc.getById(id!).pipe(
          switchMap(animal => {
            // Parallel fetch related data
            return forkJoin({
              animal: of(animal),
              healthHistory: this.healthSvc.getAnimalHealthHistory(animal.id).pipe(catchError(() => of(null))),
              farmName: animal.farmId ? this.farmSvc.getFarmById(animal.farmId).pipe(map(f => f.farmName), catchError(() => of(null))) : of(null),
              availableBatches: animal.farmId ? this.batchSvc.getBatches(animal.farmId).pipe(map(b => b.items), catchError(() => of([]))) : of([]),
              shedName: animal.shedId ? this.shedSvc.getShedById(animal.shedId).pipe(map(s => s.shedName), catchError(() => of(null))) : of(null),
              penName: animal.penId ? this.penSvc.getPenById(animal.penId).pipe(map(p => p.penNumber), catchError(() => of(null))) : of(null),
              sheds: animal.farmId ? this.shedSvc.getShedsByFarm(animal.farmId).pipe(catchError(() => of([]))) : of([]),
            }).pipe(
              switchMap(data => {
                // If there are sheds, fetch pens for all sheds to resolve movement history names
                if (data.sheds.length > 0) {
                  const penRequests = data.sheds.map(s => this.penSvc.getPensByShed(s.id).pipe(catchError(() => of([]))));
                  return forkJoin(penRequests).pipe(
                    map(pensArrays => {
                      const allPens = pensArrays.flat();
                      return { ...data, pens: allPens };
                    })
                  );
                }
                return of({ ...data, pens: [] });
              })
            );
          }),
          catchError(err => {
            this.error.set(err?.error?.detail ?? 'Not found');
            return of(null);
          })
        )
      ),
      tap(() => this.loading.set(false))
    ),
    { initialValue: null }
  );

  readonly animal = computed(() => this.animalDataResult()?.animal || null);
  readonly healthHistory = computed(() => this.animalDataResult()?.healthHistory || null);
  readonly farmName = computed(() => this.animalDataResult()?.farmName || null);
  readonly shedName = computed(() => this.animalDataResult()?.shedName || null);
  readonly penName = computed(() => this.animalDataResult()?.penName || null);
  readonly availableBatches = computed(() => this.animalDataResult()?.availableBatches || []);
  readonly sheds = computed(() => this.animalDataResult()?.sheds || []);
  readonly pens = computed(() => this.animalDataResult()?.pens || []);

  readonly resolvedMovements = computed(() => {
    const a = this.animal();
    if (!a || !a.movements) return [];
    return a.movements.map(m => ({
      ...m,
      shedName: m.shedId ? this.sheds().find(s => s.id === m.shedId)?.shedName ?? 'Unknown Shed' : '—',
      penName: m.penId ? this.pens().find(p => p.id === m.penId)?.penNumber ?? 'Unknown Pen' : '—'
    }));
  });

  statusLabel = computed(() => {
    const s = this.animal()?.status;
    return s !== undefined ? (STATUS_LABELS as any)[s] || '—' : '—';
  });

  statusClass = computed(() => {
    const status = this.animal()?.status;
    switch (status) {
      case AnimalStatus.Active: return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
      case AnimalStatus.Quarantined: return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
      case AnimalStatus.Sold: return 'bg-blue-50 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400 border border-blue-200 dark:border-blue-800';
      case AnimalStatus.Dead: return 'bg-red-50 text-red-700 dark:bg-red-900/20 dark:text-red-400 border border-red-200 dark:border-red-800';
      default: return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
    }
  });

  load(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  sortedWeights() {
    const a = this.animal();
    if (!a) return [];
    return [...a.weightRecords].sort((x, y) => y.recordedDate.localeCompare(x.recordedDate));
  }

  speciesLabel(s: number): string { return (SPECIES_LABELS as any)[s] ?? '—'; }
  sexLabel(s: number):     string { return (SEX_LABELS as any)[s]     ?? '—'; }
  tagTypeLabel(t: number): string { return t === 1 ? 'Manual' : t === 2 ? 'Ear Tag' : 'RFID'; }
  speciesEmoji(s: number): string { return s === 3 ? '🐐' : s === 4 ? '🐑' : '🐄'; }

  ageLabel(dob: string): string {
    const days = Math.floor((Date.now() - new Date(dob).getTime()) / 86_400_000);
    if (days < 365) return `${Math.floor(days / 30)}mo old`;
    return `${Math.floor(days / 365)}y old`;
  }

  onQuarantine(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(QuarantineDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId }
    });
    dialogRef.afterClosed().subscribe(res => { if (res) this.load(); });
  }

  onRelease(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: {
        title: 'Release from Quarantine',
        message: `Are you sure you want to release ${a.tagId} from quarantine?`,
        confirmButtonText: 'Release',
        isDestructive: false
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.svc.releaseFromQuarantine(a.id).subscribe({ next: () => this.load() });
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/livestock']);
  }

  onSell(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(RecordSaleDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId, latestWeightKg: a.latestWeightKg }
    });
    dialogRef.afterClosed().subscribe(res => { if (res) this.load(); });
  }

  onTransfer(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(TransferAnimalDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: {
        animalId: a.id,
        farmId: a.farmId,
        animalTag: a.tagId,
        currentShedId: a.shedId,
        currentPenId: a.penId
      }
    });
    dialogRef.afterClosed().subscribe(res => { 
      if (res) {
        this.snackBar.open('Location assigned successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      }
    });
  }

  onRecordMating(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(MatingDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.snackBar.open('Mating recorded successfully!', 'Close', { duration: 3000 });
        this.load();
      }
    });
  }

  onConfirmPregnancy(recordId: string): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(ConfirmPregnancyDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId, recordId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.snackBar.open('Pregnancy confirmed successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      }
    });
  }

  onRecordCalving(recordId: string): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(RecordCalvingDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId, recordId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.snackBar.open('Calving recorded successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      }
    });
  }

  onRecordBcs(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(RecordBcsDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.snackBar.open('BCS recorded successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      }
    });
  }

  onAssignBatch(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(AssignBatchDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { 
        animalId: a.id, 
        animalTag: a.tagId,
        currentBatchId: a.batchId,
        availableBatches: this.availableBatches()
      }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.snackBar.open('Animal assigned to batch successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      }
    });
  }

  onRecordWeight(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(RecordWeightDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.snackBar.open('Weight recorded successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      }
    });
  }

  onUploadPhoto(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(UploadPhotoDialogComponent, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.snackBar.open('Photo uploaded successfully!', 'Close', { duration: 3000, panelClass: ['snack-success'] });
        this.load();
      }
    });
  }

  private handleError(err: any): void {
    let msg = 'An unexpected error occurred.';
    if (err?.error?.errors) {
      msg = Object.values(err.error.errors).flat().join('\n');
    } else if (err?.error?.detail) {
      msg = err.error.detail;
    } else if (err?.error?.title) {
      msg = err.error.title;
    }
    this.snackBar.open(msg, 'Close', { duration: 5000, panelClass: ['snack-error'] });
  }
}
