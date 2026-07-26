import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { BatchService } from '../../services/batch.service';
import { BatchDto } from '../../models/batch.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-batch-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageHeaderComponent, MatButtonModule, MatIconModule, DatePipe],
  templateUrl: './batch-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchDetail implements OnInit {
  private readonly svc = inject(BatchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly batch = signal<BatchDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.load(id);
    }
  }

  load(id: string) {
    this.loading.set(true);
    this.svc.getBatchDetails(id).subscribe({
      next: res => {
        this.batch.set(res);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err.message);
        this.loading.set(false);
      }
    });
  }

  goBack() {
    this.router.navigate(['/livestock/batches']);
  }
}
