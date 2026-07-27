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
import { VaccinationProtocolDto } from '../../models/health.models';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { AssignProtocolDialog } from '../../components/dialogs/assign-protocol-dialog/assign-protocol-dialog.component';

@Component({
  selector: 'app-vaccination-protocol-list',
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
  templateUrl: './vaccination-protocol-list.html',
  styleUrls: ['./vaccination-protocol-list.scss']
})
export class VaccinationProtocolListComponent implements OnInit {
  private healthService = inject(HealthService);
  private dialog = inject(MatDialog);

  displayedColumns: string[] = ['title', 'targetSpecies', 'steps', 'status', 'actions'];
  dataSource: VaccinationProtocolDto[] = [];
  totalItems = 0;
  pageSize = 10;
  pageIndex = 0;
  isLoading = true;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit(): void {
    this.loadProtocols();
  }

  loadProtocols(): void {
    this.isLoading = true;
    const pageNumber = this.pageIndex + 1;
    this.healthService.getVaccinationProtocols(pageNumber, this.pageSize).subscribe({
      next: (response) => {
        this.dataSource = response.items;
        this.totalItems = response.totalCount;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error loading protocols', err);
        this.isLoading = false;
      }
    });
  }

  onPageChange(event: any): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadProtocols();
  }

  openAssignDialog(protocol: VaccinationProtocolDto): void {
    const dialogRef = this.dialog.open(AssignProtocolDialog, {
      width: '600px',
      data: { protocol }
    });
    dialogRef.afterClosed().subscribe(result => {
      // Could show success toast
    });
  }
}
