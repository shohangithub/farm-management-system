import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { HealthService } from '../../services/health.service';
import { VaccinationProtocolDto } from '../../models/health.models';

@Component({
  selector: 'app-vaccination-protocol-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatListModule,
    MatDividerModule
  ],
  templateUrl: './vaccination-protocol-detail.html',
  styleUrls: ['./vaccination-protocol-detail.scss']
})
export class VaccinationProtocolDetailComponent implements OnInit {
  private healthService = inject(HealthService);
  private route = inject(ActivatedRoute);

  protocol: VaccinationProtocolDto | null = null;
  isLoading = true;
  error = '';

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadProtocol(id);
    } else {
      this.error = 'Invalid protocol ID';
      this.isLoading = false;
    }
  }

  loadProtocol(id: string): void {
    this.isLoading = true;
    this.healthService.getVaccinationProtocol(id).subscribe({
      next: (data) => {
        this.protocol = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error loading protocol details', err);
        this.error = 'Failed to load protocol details.';
        this.isLoading = false;
      }
    });
  }
}
