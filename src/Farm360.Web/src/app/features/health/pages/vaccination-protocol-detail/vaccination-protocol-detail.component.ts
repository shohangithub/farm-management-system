import { Component, inject, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { map, switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';

import { HealthService } from '../../services/health.service';
import { VaccinationProtocolDto } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { CreateProtocolDialogComponent } from '../../components/dialogs/create-protocol-dialog/create-protocol-dialog.component';
import { AssignProtocolDialog } from '../../components/dialogs/assign-protocol-dialog/assign-protocol-dialog.component';

@Component({
  selector: 'app-vaccination-protocol-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    LoadingComponent,
    MatDialogModule
  ],
  templateUrl: './vaccination-protocol-detail.html',
  styleUrls: ['./vaccination-protocol-detail.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VaccinationProtocolDetailComponent {
  private healthService = inject(HealthService);
  private route = inject(ActivatedRoute);
  private dialog = inject(MatDialog);

  // --- Reactive State (Signals) ---
  isLoading = signal(true);
  error = signal('');
  refreshTrigger = signal(0);

  // Derive route param ID as a Signal
  private protocolId = toSignal(
    this.route.paramMap.pipe(map(params => params.get('id'))),
    { initialValue: null }
  );

  // Combined trigger for fetching data
  private fetchParams = computed(() => ({
    id: this.protocolId(),
    refresh: this.refreshTrigger()
  }));

  // Reactive Protocol Resource
  protocol = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => {
        this.isLoading.set(true);
        this.error.set('');
      }),
      switchMap(({ id }) => {
        if (!id) {
          this.error.set('Invalid protocol ID');
          this.isLoading.set(false);
          return of(null);
        }
        return this.healthService.getVaccinationProtocol(id).pipe(
          catchError((err) => {
            console.error('Error loading protocol details', err);
            this.error.set('Failed to load protocol details.');
            return of(null);
          }),
          tap(() => this.isLoading.set(false))
        );
      })
    ),
    { initialValue: null }
  );

  totalDurationDays = computed(() => {
    const p = this.protocol();
    if (!p || !p.steps || p.steps.length === 0) return 0;
    return Math.max(...p.steps.map(s => s.targetAgeDays));
  });

  // --- Actions ---
  loadProtocol(): void {
    // Just trigger the signal pipeline
    this.refreshTrigger.update(v => v + 1);
  }

  openEditDialog(): void {
    const p = this.protocol();
    if (!p) return;
    
    const dialogRef = this.dialog.open(CreateProtocolDialogComponent, {
      width: '700px',
      maxWidth: '95vw',
      data: p
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadProtocol();
    });
  }

  openAssignDialog(): void {
    const p = this.protocol();
    if (!p) return;
    
    this.dialog.open(AssignProtocolDialog, {
      width: '600px',
      data: { protocol: p }
    });
  }
}
