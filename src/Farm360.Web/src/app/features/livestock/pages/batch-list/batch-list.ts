import { Component, OnInit, inject, signal, ChangeDetectionStrategy, ViewChild, TemplateRef } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { BatchService } from '../../services/batch.service';
import { BatchDto, BatchStatus } from '../../models/batch.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { FarmService } from '../../../farms/services/farm.service';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'app-batch-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageHeaderComponent, FormsModule, MatButtonModule, MatIconModule, MatDialogModule, MatSnackBarModule],
  templateUrl: './batch-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchList implements OnInit {
  private readonly svc = inject(BatchService);
  private readonly farmSvc = inject(FarmService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly batches = signal<BatchDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  @ViewChild('createBatchDialog') createBatchDialog!: TemplateRef<any>;
  
  createForm = {
    name: '',
    notes: '',
    farmId: ''
  };
  
  farms: any[] = [];

  ngOnInit() {
    this.farmSvc.getAllFarms().subscribe(f => {
      this.farms = f;
      if (f.length > 0) {
        this.createForm.farmId = f[0].id;
        this.load(this.createForm.farmId);
      } else {
        this.loading.set(false);
      }
    });
  }

  load(farmId: string) {
    this.loading.set(true);
    this.svc.getBatches(farmId).subscribe({
      next: res => {
        this.batches.set(res.items);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err.message);
        this.loading.set(false);
      }
    });
  }

  onCreateBatch() {
    this.createForm.name = '';
    this.createForm.notes = '';
    this.dialog.open(this.createBatchDialog, { width: '400px' });
  }

  submitCreateBatch() {
    if (!this.createForm.name || !this.createForm.farmId) return;

    this.svc.createBatch({
      farmId: this.createForm.farmId,
      name: this.createForm.name,
      notes: this.createForm.notes
    }).subscribe({
      next: () => {
        this.dialog.closeAll();
        this.snackBar.open('Batch created successfully', 'Close', { duration: 3000 });
        this.load(this.createForm.farmId);
      }
    });
  }

  onFarmChange(event: any) {
    const farmId = event.target.value;
    if (farmId) {
      this.createForm.farmId = farmId;
      this.load(farmId);
    }
  }
}
