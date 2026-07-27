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
import { HealthService } from '../../services/health.service';
import { MedicalTreatmentDto, TreatmentStatus } from '../../models/health.models';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { LogTreatmentDialog } from '../../components/dialogs/log-treatment-dialog/log-treatment-dialog.component';
import { MatMenuModule } from '@angular/material/menu';

@Component({
  selector: 'app-treatment-list',
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
    MatDialogModule,
    MatMenuModule
  ],
  templateUrl: './treatment-list.html',
  styleUrls: ['./treatment-list.scss']
})
export class TreatmentListComponent implements OnInit {
  private healthService = inject(HealthService);
  private dialog = inject(MatDialog);

  displayedColumns: string[] = ['animalId', 'diagnosis', 'medicationName', 'startDate', 'status', 'cost', 'actions'];
  dataSource: MedicalTreatmentDto[] = [];
  totalItems = 0;
  pageSize = 10;
  pageIndex = 0;
  isLoading = true;
  treatmentStatus = TreatmentStatus;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit(): void {
    this.loadTreatments();
  }

  loadTreatments(): void {
    this.isLoading = true;
    const pageNumber = this.pageIndex + 1;
    this.healthService.getTreatments(pageNumber, this.pageSize).subscribe({
      next: (response) => {
        this.dataSource = response.items;
        this.totalItems = response.totalCount;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error loading treatments', err);
        this.isLoading = false;
      }
    });
  }

  onPageChange(event: any): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadTreatments();
  }

  openLogTreatmentDialog(): void {
    const dialogRef = this.dialog.open(LogTreatmentDialog, {
      width: '600px'
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadTreatments();
    });
  }

  updateStatus(treatment: MedicalTreatmentDto, status: TreatmentStatus): void {
    this.healthService.updateTreatmentStatus(treatment.id, status, 'Status updated via list view').subscribe({
      next: () => this.loadTreatments(),
      error: (err) => console.error('Error updating status', err)
    });
  }
}
