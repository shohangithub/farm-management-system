import {
  Component, OnInit, OnDestroy, inject, signal, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule }        from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { Subject, takeUntil }  from 'rxjs';
import { AnimalService }       from '../../services/animal.service';
import {
  AnimalDto, AnimalStatus, AnimalSex,
  SPECIES_LABELS, STATUS_LABELS, STATUS_BADGE_CLASS, SEX_LABELS,
} from '../../models/animal.models';

@Component({
  selector: 'app-animal-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule],
  template: `
    <!-- ── Skeleton loading ───────────────────────────────── -->
    <div *ngIf="loading()">
      <div class="skeleton" style="height:32px;width:300px;margin-bottom:24px;"></div>
      <div class="skeleton" style="height:200px;border-radius:12px;margin-bottom:16px;"></div>
      <div class="skeleton" style="height:120px;border-radius:12px;"></div>
    </div>

    <!-- ── Error ──────────────────────────────────────────── -->
    <div *ngIf="!loading() && error()" class="empty-state">
      <div class="empty-icon">⚠️</div>
      <h3>Animal not found</h3>
      <p>{{ error() }}</p>
      <a routerLink="/livestock" class="btn btn-primary btn-sm">Back to list</a>
    </div>

    <!-- ── Content ────────────────────────────────────────── -->
    <ng-container *ngIf="!loading() && animal() as a">

      <!-- Breadcrumb + Header -->
      <div class="page-header">
        <div>
          <nav class="breadcrumb">
            <a routerLink="/">Home</a>
            <span class="separator">›</span>
            <a routerLink="/livestock">Livestock</a>
            <span class="separator">›</span>
            <span>{{ a.tagId }}</span>
          </nav>
          <h1 class="page-title" style="display:flex;align-items:center;gap:12px">
            {{ a.tagId }}
            <span class="badge" [ngClass]="statusBadge(a.status)">{{ statusLabel(a.status) }}</span>
          </h1>
          <p class="page-subtitle">{{ speciesLabel(a.species) }} · {{ a.breedName }} · {{ sexLabel(a.sex) }}</p>
        </div>
        <div class="d-flex gap-3 align-center">
          <button class="btn btn-secondary" (click)="load()" title="Refresh">↻ Refresh</button>
          <a [routerLink]="['/livestock', a.id, 'weights', 'new']" class="btn btn-primary">+ Record Weight</a>
        </div>
      </div>

      <!-- ── Top section: photo + identity + quick stats ─── -->
      <div style="display:grid;grid-template-columns:auto 1fr;gap:20px;margin-bottom:20px;align-items:start">

        <!-- Photo -->
        <div class="card" style="width:200px;min-height:200px;display:flex;align-items:center;justify-content:center;overflow:hidden">
          <img *ngIf="a.primaryPhotoUrl"
               [src]="a.primaryPhotoUrl"
               style="width:100%;height:200px;object-fit:cover"
               [alt]="a.tagId" />
          <div *ngIf="!a.primaryPhotoUrl" style="font-size:4rem;opacity:0.3">
            {{ speciesEmoji(a.species) }}
          </div>
        </div>

        <!-- Identity card -->
        <div class="card card--elevated">
          <div class="card-header">
            <h3 style="font-size:1rem">Animal Profile</h3>
            <div class="d-flex gap-2">
              <button *ngIf="a.status === AnimalStatus.Active"
                class="btn btn-secondary btn-sm" (click)="onQuarantine(a)">⚠ Quarantine</button>
              <button *ngIf="a.status === AnimalStatus.Quarantined"
                class="btn btn-secondary btn-sm" (click)="onRelease(a)">✓ Release</button>
              <button *ngIf="a.status === AnimalStatus.Active"
                class="btn btn-primary btn-sm" (click)="onSell(a)">💰 Sell</button>
            </div>
          </div>
          <div class="card-body">
            <div class="detail-grid">
              <div class="detail-row">
                <span class="detail-label">Tag ID</span>
                <span class="detail-value fw-600">{{ a.tagId }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Tag Type</span>
                <span class="detail-value">{{ tagTypeLabel(a.tagType) }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Species</span>
                <span class="detail-value">{{ speciesLabel(a.species) }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Breed</span>
                <span class="detail-value">{{ a.breedName }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Sex</span>
                <span class="detail-value">{{ sexLabel(a.sex) }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Date of Birth</span>
                <span class="detail-value">{{ a.dateOfBirth | date:'dd MMM yyyy' }} · {{ ageLabel(a.dateOfBirth) }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Farm</span>
                <span class="detail-value text-muted">{{ a.farmId }}</span>
              </div>
              <div class="detail-row" *ngIf="a.acquisitionPriceBdt">
                <span class="detail-label">Purchase Price</span>
                <span class="detail-value">৳ {{ a.acquisitionPriceBdt | number }}</span>
              </div>
              <div class="detail-row" *ngIf="a.notes">
                <span class="detail-label">Notes</span>
                <span class="detail-value text-muted">{{ a.notes }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ── KPI row ─────────────────────────────────────── -->
      <div class="stat-grid" style="margin-bottom:20px">
        <div class="stat-card">
          <div class="stat-label">Latest Weight</div>
          <div class="stat-value text-accent">
            {{ a.latestWeightKg ? (a.latestWeightKg | number:'1.1-1') + ' kg' : '—' }}
          </div>
          <div class="stat-delta" *ngIf="a.latestWeightDate">
            as of {{ a.latestWeightDate | date:'dd MMM' }}
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Avg Daily Gain</div>
          <div class="stat-value" [class.text-accent]="(a.adgKgPerDay ?? 0) > 0">
            {{ a.adgKgPerDay ? (a.adgKgPerDay | number:'1.3-3') + ' kg/d' : '—' }}
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Weight Records</div>
          <div class="stat-value">{{ a.weightRecords.length }}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Breeding Records</div>
          <div class="stat-value">{{ a.breedingRecords.length }}</div>
        </div>
      </div>

      <!-- ── Weight History Table ─────────────────────────── -->
      <div class="card" style="margin-bottom:20px">
        <div class="card-header">
          <h3 style="font-size:1rem">Weight History</h3>
          <a [routerLink]="['/livestock', a.id, 'weights', 'new']" class="btn btn-primary btn-sm">
            + Record Weight
          </a>
        </div>
        <div *ngIf="a.weightRecords.length === 0" class="empty-state" style="padding:40px">
          <div class="empty-icon">⚖️</div>
          <h3>No weight records yet</h3>
          <p>Record the first weight measurement</p>
        </div>
        <div *ngIf="a.weightRecords.length > 0" style="overflow-x:auto">
          <table class="data-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Weight (kg)</th>
                <th>Recorded At</th>
                <th>Notes</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let w of sortedWeights(a)">
                <td class="fw-500">{{ w.recordedDate | date:'dd MMM yyyy' }}</td>
                <td class="fw-600 text-accent">{{ w.weightKg | number:'1.1-1' }} kg</td>
                <td class="text-muted text-xs">{{ w.recordedAtUtc | date:'dd MMM yyyy, HH:mm' }}</td>
                <td class="text-muted text-sm">{{ w.notes ?? '—' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- ── Photos ─────────────────────────────────────────── -->
      <div class="card">
        <div class="card-header">
          <h3 style="font-size:1rem">Photos</h3>
        </div>
        <div *ngIf="a.photos.length === 0" class="empty-state" style="padding:40px">
          <div class="empty-icon">📷</div>
          <h3>No photos yet</h3>
          <p>Add a photo to the animal profile</p>
        </div>
        <div *ngIf="a.photos.length > 0" class="card-body" style="display:flex;flex-wrap:wrap;gap:12px">
          <div *ngFor="let p of a.photos"
               style="position:relative;border-radius:8px;overflow:hidden;border:2px solid transparent"
               [style.border-color]="p.isPrimary ? 'var(--color-primary)' : 'var(--border-subtle)'">
            <img [src]="p.photoUrl" [alt]="p.caption ?? 'Photo'"
                 style="width:120px;height:120px;object-fit:cover" />
            <div *ngIf="p.isPrimary"
                 style="position:absolute;bottom:4px;left:4px;font-size:0.65rem;background:var(--color-primary);color:#fff;padding:2px 6px;border-radius:4px">
              Primary
            </div>
          </div>
        </div>
      </div>

    </ng-container>
  `,
  styles: [`
    .detail-grid { display: flex; flex-direction: column; gap: 0; }
    .detail-row {
      display: flex;
      align-items: baseline;
      gap: 12px;
      padding: 10px 0;
      border-bottom: 1px solid var(--border-subtle);
      &:last-child { border-bottom: none; }
    }
    .detail-label {
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      min-width: 140px;
    }
    .detail-value {
      font-size: 0.875rem;
      color: var(--text-secondary);
    }
  `],
})
export class AnimalDetailComponent implements OnInit, OnDestroy {
  private readonly svc     = inject(AnimalService);
  private readonly route   = inject(ActivatedRoute);
  private readonly destroy$ = new Subject<void>();

  readonly loading = signal(true);
  readonly error   = signal<string | null>(null);
  readonly animal  = signal<AnimalDto | null>(null);

  readonly AnimalStatus = AnimalStatus;

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loading.set(true);
    this.error.set(null);

    this.svc.getById(id).pipe(takeUntil(this.destroy$)).subscribe({
      next:  a  => { this.animal.set(a); this.loading.set(false); },
      error: e  => { this.error.set(e?.error?.detail ?? 'Not found'); this.loading.set(false); }
    });
  }

  sortedWeights(a: AnimalDto) {
    return [...a.weightRecords].sort((x, y) => y.recordedDate.localeCompare(x.recordedDate));
  }

  speciesLabel(s: number): string { return (SPECIES_LABELS as any)[s] ?? '—'; }
  statusLabel(s: number):  string { return (STATUS_LABELS as any)[s]  ?? '—'; }
  sexLabel(s: number):     string { return (SEX_LABELS as any)[s]     ?? '—'; }
  statusBadge(s: number):  string { return (STATUS_BADGE_CLASS as any)[s] ?? 'badge-default'; }
  tagTypeLabel(t: number): string { return t === 1 ? 'Manual' : t === 2 ? 'Ear Tag' : 'RFID'; }
  speciesEmoji(s: number): string { return s === 3 ? '🐐' : s === 4 ? '🐑' : '🐄'; }

  ageLabel(dob: string): string {
    const days = Math.floor((Date.now() - new Date(dob).getTime()) / 86_400_000);
    if (days < 365) return `${Math.floor(days / 30)}mo old`;
    return `${Math.floor(days / 365)}y old`;
  }

  onQuarantine(a: AnimalDto): void {
    const reason = prompt('Quarantine reason:');
    if (!reason) return;
    this.svc.quarantine(a.id, { reason }).pipe(takeUntil(this.destroy$)).subscribe({ next: () => this.load() });
  }

  onRelease(a: AnimalDto): void {
    if (!confirm('Release from quarantine?')) return;
    this.svc.releaseFromQuarantine(a.id).pipe(takeUntil(this.destroy$)).subscribe({ next: () => this.load() });
  }

  onSell(a: AnimalDto): void {
    const price = prompt('Sale price (BDT):');
    if (!price || isNaN(+price)) return;
    const today = new Date().toISOString().split('T')[0];
    this.svc.sell(a.id, { salePriceBdt: +price, saleDate: today })
      .pipe(takeUntil(this.destroy$)).subscribe({ next: () => this.load() });
  }
}
