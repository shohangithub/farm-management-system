import { Component, inject, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HealthService } from '../../services/health.service';
import { VetVisitDto } from '../../models/health.models';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

@Component({
  selector: 'app-vet-visit-list',
  standalone: true,
  imports: [
    CommonModule, 
    RouterModule, 
    MatCardModule, 
    MatButtonModule, 
    MatIconModule, 
    MatTableModule, 
    MatPaginatorModule, 
    MatChipsModule, 
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDialogModule
  ],
  templateUrl: './vet-visit-list.html',
  styleUrls: ['./vet-visit-list.scss']
})
export class VetVisitListComponent implements OnInit {
  private healthService = inject(HealthService);
  private dialog = inject(MatDialog);

  displayedColumns: string[] = ['visitDate', 'vetName', 'visitType', 'purpose', 'cost', 'nextVisit', 'actions'];
  dataSource: VetVisitDto[] = [];
  totalItems = 0;
  pageSize = 10;
  pageIndex = 0;
  isLoading = true;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit(): void {
    this.loadVetVisits();
  }

  loadVetVisits(): void {
    this.isLoading = true;
    const pageNumber = this.pageIndex + 1;
    this.healthService.getVetVisits(pageNumber, this.pageSize).subscribe({
      next: (response) => {
        this.dataSource = response.items;
        this.totalItems = response.totalCount;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error loading vet visits', err);
        this.isLoading = false;
      }
    });
  }

  onPageChange(event: any): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadVetVisits();
  }

  openScheduleVisitDialog(): void {
    // We could create a ScheduleVetVisitDialog if required, or skip if out of scope
  }
}
