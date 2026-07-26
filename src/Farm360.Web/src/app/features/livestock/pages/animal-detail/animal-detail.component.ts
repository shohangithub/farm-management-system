import {
  Component, OnInit, OnDestroy, inject, signal, ChangeDetectionStrategy, computed,
  TemplateRef, ViewChild
} from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil }  from 'rxjs';
import { AnimalService }       from '../../services/animal.service';
import { ShedService }       from '../../../farms/services/shed.service';
import { PenService }        from '../../../farms/services/pen.service';
import { HealthService }     from '../../../health/services/health.service';
import { ShedList }          from '../../../farms/models/shed.model';
import { PenList }           from '../../../farms/models/pen.model';
import { AnimalHealthHistoryDto, VaccinationStatus, TreatmentStatus } from '../../../health/models/health.models';
import {
  AnimalDto, AnimalStatus, AnimalSex, AnimalMovementDto,
  SPECIES_LABELS, STATUS_LABELS, SEX_LABELS,
} from '../../models/animal.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FarmService } from '../../../farms/services/farm.service';
import { BatchService } from '../../services/batch.service';
import { BatchDto } from '../../models/batch.models';

@Component({
  selector: 'app-animal-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterModule, FormsModule,
    PageHeaderComponent, DatePipe, DecimalPipe,
    MatButtonModule, MatIconModule, MatDialogModule, MatTabsModule, MatSnackBarModule
  ],
  templateUrl: './animal-detail.component.html'
})
export class AnimalDetailComponent implements OnInit, OnDestroy {
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
  private readonly destroy$ = new Subject<void>();

  readonly loading = signal(true);
  readonly error   = signal<string | null>(null);
  readonly animal  = signal<AnimalDto | null>(null);
  readonly healthHistory = signal<AnimalHealthHistoryDto | null>(null);

  readonly farmName = signal<string | null>(null);
  readonly shedName = signal<string | null>(null);
  readonly penName = signal<string | null>(null);

  readonly availableBatches = signal<BatchDto[]>([]);

  readonly AnimalStatus = AnimalStatus;
  readonly AnimalSex = AnimalSex;
  readonly VaccinationStatus = VaccinationStatus;
  readonly TreatmentStatus = TreatmentStatus;

  readonly Math = Math;

  @ViewChild('saleDialogTemplate') saleDialogTemplate!: TemplateRef<any>;
  saleForm = {
    price: null as number | null,
    date: new Date().toISOString().split('T')[0],
    buyer: '',
    weight: null as number | null
  };

  @ViewChild('quarantineDialogTemplate') quarantineDialogTemplate!: TemplateRef<any>;
  quarantineForm = {
    reason: ''
  };

  @ViewChild('transferDialogTemplate') transferDialogTemplate!: TemplateRef<any>;
  transferForm = {
    shedId: '',
    penId: '',
    date: new Date().toISOString().split('T')[0],
    reason: ''
  };

  @ViewChild('matingDialogTemplate') matingDialogTemplate!: TemplateRef<any>;
  matingForm = {
    date: new Date().toISOString().split('T')[0],
    isAI: false,
    sireAnimalId: '',
    sireExternalId: ''
  };

  @ViewChild('confirmPregnancyDialogTemplate') confirmPregnancyDialogTemplate!: TemplateRef<any>;
  confirmPregnancyForm = {
    recordId: '',
    confirmDate: new Date().toISOString().split('T')[0],
    expectedCalvingDate: '',
    error: null as string | null
  };

  @ViewChild('recordCalvingDialogTemplate') recordCalvingDialogTemplate!: TemplateRef<any>;
  recordCalvingForm = {
    recordId: '',
    calvingDate: new Date().toISOString().split('T')[0],
    outcome: 'Live Birth',
    calvesCount: 1
  };

  @ViewChild('assignBatchDialogTemplate') assignBatchDialogTemplate!: TemplateRef<any>;
  assignBatchForm = {
    batchId: ''
  };

  @ViewChild('recordBcsDialogTemplate') recordBcsDialogTemplate!: TemplateRef<any>;
  recordBcsForm = {
    score: null as number | null,
    recordedDate: new Date().toISOString().split('T')[0],
    notes: ''
  };

  readonly sheds = signal<ShedList[]>([]);
  readonly pens = signal<PenList[]>([]);
  readonly loadingSheds = signal(false);
  readonly loadingPens = signal(false);

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

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loading.set(true);
    this.error.set(null);

    this.svc.getById(id).pipe(takeUntil(this.destroy$)).subscribe({
      next:  a  => { 
        this.animal.set(a); 
        
        // Fetch Health History in parallel
        this.healthSvc.getAnimalHealthHistory(id).subscribe({
          next: h => this.healthHistory.set(h),
          error: () => this.healthHistory.set(null)
        });

        this.loading.set(false); 
        
        // Load Location Names and Batches
        if (a.farmId) {
          this.farmSvc.getFarmById(a.farmId).subscribe(f => this.farmName.set(f.farmName));
          
          this.batchSvc.getBatches(a.farmId).subscribe(b => {
             this.availableBatches.set(b.items);
          });
          
          // Preload Sheds and Pens for the farm to resolve movement history names
          this.shedSvc.getShedsByFarm(a.farmId).subscribe(sheds => {
            this.sheds.set(sheds);
            sheds.forEach(s => {
              this.penSvc.getPensByShed(s.id).subscribe(pens => {
                this.pens.update(existing => {
                  // Only add pens that aren't already in the list
                  const newPens = pens.filter(p => !existing.some(ep => ep.id === p.id));
                  return [...existing, ...newPens];
                });
              });
            });
          });
        }
        if (a.shedId) {
          this.shedSvc.getShedById(a.shedId).subscribe(s => this.shedName.set(s.shedName));
        } else {
          this.shedName.set(null);
        }
        if (a.penId) {
          this.penSvc.getPenById(a.penId).subscribe(p => this.penName.set(p.penNumber));
        } else {
          this.penName.set(null);
        }
      },
      error: e  => { this.error.set(e?.error?.detail ?? 'Not found'); this.loading.set(false); }
    });
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
    
    this.quarantineForm = { reason: '' };

    this.dialog.open(this.quarantineDialogTemplate, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent'
    });
  }

  confirmQuarantine(): void {
    const a = this.animal();
    if (!a || !this.quarantineForm.reason) return;

    this.svc.quarantine(a.id, { reason: this.quarantineForm.reason }).pipe(takeUntil(this.destroy$)).subscribe({ 
      next: () => {
        this.dialog.closeAll();
        this.load();
      }
    });
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

    dialogRef.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(result => {
      if (result) {
        this.svc.releaseFromQuarantine(a.id).pipe(takeUntil(this.destroy$)).subscribe({ next: () => this.load() });
      }
    });
  }

  onRecordWeight(): void {
    const a = this.animal();
    if (!a) return;
    this.router.navigate(['/livestock', a.id, 'weights', 'new']);
  }

  goBack(): void {
    this.router.navigate(['/livestock']);
  }

  onSell(): void {
    const a = this.animal();
    if (!a) return;
    
    // Reset form
    this.saleForm = {
      price: null,
      date: new Date().toISOString().split('T')[0],
      buyer: '',
      weight: a.latestWeightKg ?? null
    };

    this.dialog.open(this.saleDialogTemplate, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent' // if relying entirely on internal tailwind
    });
  }

  confirmSale(): void {
    const a = this.animal();
    if (!a || !this.saleForm.price) return;
    
    this.svc.sell(a.id, { 
      salePriceBdt: this.saleForm.price, 
      saleDate: this.saleForm.date,
      buyerName: this.saleForm.buyer || undefined,
      saleWeightKg: this.saleForm.weight || undefined
    }).pipe(takeUntil(this.destroy$)).subscribe({ 
      next: () => {
        this.dialog.closeAll();
        this.load();
      }
    });
  }

  onTransfer(): void {
    const a = this.animal();
    if (!a) return;
    
    this.transferForm = {
      shedId: a.shedId || '',
      penId: a.penId || '',
      date: new Date().toISOString().split('T')[0],
      reason: ''
    };
    
    this.sheds.set([]);
    this.pens.set([]);
    
    this.loadingSheds.set(true);
    this.shedSvc.getShedsByFarm(a.farmId).subscribe({
      next: (data) => {
        this.sheds.set(data);
        this.loadingSheds.set(false);
        if (this.transferForm.shedId) {
          this.loadPensForTransfer(this.transferForm.shedId);
        }
      },
      error: () => this.loadingSheds.set(false)
    });

    this.dialog.open(this.transferDialogTemplate, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent'
    });
  }

  onTransferShedChange(shedId: string): void {
    this.transferForm.shedId = shedId;
    this.transferForm.penId = '';
    if (shedId) {
      this.loadPensForTransfer(shedId);
    } else {
      this.pens.set([]);
    }
  }

  private loadPensForTransfer(shedId: string): void {
    this.loadingPens.set(true);
    this.penSvc.getPensByShed(shedId).subscribe({
      next: (data) => {
        this.pens.set(data);
        this.loadingPens.set(false);
      },
      error: () => this.loadingPens.set(false)
    });
  }

  confirmTransfer(): void {
    const a = this.animal();
    if (!a || !this.transferForm.date) return;
    
    this.svc.transfer(a.id, {
      toShedId: this.transferForm.shedId || undefined,
      toPenId: this.transferForm.penId || undefined,
      transferDate: this.transferForm.date,
      reason: this.transferForm.reason || undefined
    }).subscribe({
      next: () => {
        this.dialog.closeAll();
        this.snackBar.open('Location assigned successfully!', 'Close', { 
          duration: 3000,
          panelClass: ['success-snackbar']
        });
        this.load();
      }
    });
  }

  onRecordMating(): void {
    const a = this.animal();
    if (!a) return;
    
    this.matingForm = {
      date: new Date().toISOString().split('T')[0],
      isAI: false,
      sireAnimalId: '',
      sireExternalId: ''
    };

    this.dialog.open(this.matingDialogTemplate, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent'
    });
  }

  confirmMating(): void {
    const a = this.animal();
    if (!a || !this.matingForm.date) return;
    
    this.svc.recordMating(a.id, {
      matingDate: this.matingForm.date,
      isArtificialInsemination: this.matingForm.isAI,
      sireAnimalId: this.matingForm.sireAnimalId || undefined,
      sireExternalId: this.matingForm.sireExternalId || undefined
    }).subscribe({
      next: () => {
        this.dialog.closeAll();
        this.snackBar.open('Mating recorded successfully!', 'Close', { duration: 3000 });
        this.load();
      }
    });
  }

  onConfirmPregnancy(recordId: string): void {
    const a = this.animal();
    if (!a) return;
    
    // Estimate expected calving date: roughly 283 days for cows, adjusting based on species can be done if needed.
    const expected = new Date();
    expected.setDate(expected.getDate() + 283);

    this.confirmPregnancyForm = {
      recordId: recordId,
      confirmDate: new Date().toISOString().split('T')[0],
      expectedCalvingDate: expected.toISOString().split('T')[0],
      error: null
    };

    this.dialog.open(this.confirmPregnancyDialogTemplate, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent'
    });
  }

  submitConfirmPregnancy(): void {
    const a = this.animal();
    if (!a || !this.confirmPregnancyForm.confirmDate || !this.confirmPregnancyForm.expectedCalvingDate) return;

    this.confirmPregnancyForm.error = null;

    this.svc.confirmPregnancy(a.id, this.confirmPregnancyForm.recordId, {
      confirmDate: this.confirmPregnancyForm.confirmDate,
      expectedCalvingDate: this.confirmPregnancyForm.expectedCalvingDate
    }).subscribe({
      next: () => {
        this.dialog.closeAll();
        this.snackBar.open('Pregnancy confirmed successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      },
      error: err => {
        this.confirmPregnancyForm.error = err.error?.detail || err.error?.title || 'An error occurred while confirming pregnancy.';
      }
    });
  }

  onRecordCalving(recordId: string): void {
    const a = this.animal();
    if (!a) return;
    
    this.recordCalvingForm = {
      recordId: recordId,
      calvingDate: new Date().toISOString().split('T')[0],
      outcome: 'Live Birth',
      calvesCount: 1
    };

    this.dialog.open(this.recordCalvingDialogTemplate, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent'
    });
  }

  submitRecordCalving(): void {
    const a = this.animal();
    if (!a || !this.recordCalvingForm.calvingDate) return;

    this.svc.recordCalving(a.id, this.recordCalvingForm.recordId, {
      calvingDate: this.recordCalvingForm.calvingDate,
      outcome: this.recordCalvingForm.outcome,
      calvesCount: this.recordCalvingForm.calvesCount
    }).subscribe({
      next: () => {
        this.dialog.closeAll();
        this.snackBar.open('Calving recorded successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      }
    });
  }

  onRecordBcs(): void {
    const a = this.animal();
    if (!a) return;

    this.recordBcsForm = {
      score: null,
      recordedDate: new Date().toISOString().split('T')[0],
      notes: ''
    };

    this.dialog.open(this.recordBcsDialogTemplate, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent'
    });
  }

  submitRecordBcs(): void {
    const a = this.animal();
    if (!a || !this.recordBcsForm.score || !this.recordBcsForm.recordedDate) return;

    this.svc.recordBcs(a.id, this.recordBcsForm.score, this.recordBcsForm.recordedDate, this.recordBcsForm.notes).subscribe({
      next: () => {
        this.dialog.closeAll();
        this.snackBar.open('BCS recorded successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      }
    });
  }

  onAssignBatch(): void {
    const a = this.animal();
    if (!a) return;

    this.assignBatchForm = {
      batchId: a.batchId ?? ''
    };

    this.dialog.open(this.assignBatchDialogTemplate, {
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent'
    });
  }

  submitAssignBatch(): void {
    const a = this.animal();
    if (!a || !this.assignBatchForm.batchId) return;

    this.batchSvc.assignAnimalsToBatch(this.assignBatchForm.batchId, [a.id]).subscribe({
      next: () => {
        this.dialog.closeAll();
        this.snackBar.open('Animal assigned to batch successfully!', 'Close', { duration: 3000, panelClass: ['success-snackbar'] });
        this.load();
      }
    });
  }
}
