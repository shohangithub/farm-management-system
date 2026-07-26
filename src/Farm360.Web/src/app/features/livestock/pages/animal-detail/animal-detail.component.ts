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
import { ShedList }          from '../../../farms/models/shed.model';
import { PenList }           from '../../../farms/models/pen.model';
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
  private readonly dialog  = inject(MatDialog);
  private readonly router  = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroy$ = new Subject<void>();

  readonly loading = signal(true);
  readonly error   = signal<string | null>(null);
  readonly animal  = signal<AnimalDto | null>(null);

  readonly farmName = signal<string | null>(null);
  readonly shedName = signal<string | null>(null);
  readonly penName = signal<string | null>(null);

  readonly AnimalStatus = AnimalStatus;
  readonly AnimalSex = AnimalSex;

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
        this.loading.set(false); 
        
        // Load Location Names
        if (a.farmId) {
          this.farmSvc.getFarmById(a.farmId).subscribe(f => this.farmName.set(f.farmName));
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
}
