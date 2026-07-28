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
import { MortalityRecordDto } from '../../models/health.models';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { RecordMortalityDialog } from '../../components/dialogs/record-mortality-dialog/record-mortality-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-mortality-list',
  standalone: true,
  imports: [
    CommonModule, 
    RouterModule, 
    MatButtonModule, 
    MatIconModule, 
    MatPaginatorModule, 
    MatTooltipModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  templateUrl: './mortality-list.html',
  styleUrls: ['./mortality-list.scss']
})
export class MortalityListComponent implements OnInit {
  private healthService = inject(HealthService);
  private dialog = inject(MatDialog);

  displayedColumns: string[] = ['animalId', 'deathDate', 'causeOfDeath', 'diseaseName', 'loss', 'actions'];
  dataSource: MortalityRecordDto[] = [];
  totalItems = 0;
  pageSize = 10;
  pageIndex = 0;
  isLoading = true;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit(): void {
    this.loadMortalities();
  }

  loadMortalities(): void {
    this.isLoading = true;
    const pageNumber = this.pageIndex + 1;
    this.healthService.getMortalityRecords(pageNumber, this.pageSize).subscribe({
      next: (response) => {
        this.dataSource = response.items;
        this.totalItems = response.totalCount;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error loading mortality records', err);
        this.isLoading = false;
      }
    });
  }

  onPageChange(event: any): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadMortalities();
  }

  openRecordMortalityDialog(): void {
    const dialogRef = this.dialog.open(RecordMortalityDialog, {
      width: '500px'
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadMortalities();
    });
  }
}
