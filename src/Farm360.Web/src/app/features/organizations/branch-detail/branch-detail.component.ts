import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { BranchService } from '../services/branch.service';
import { Branch } from '../models/branch.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-branch-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageHeaderComponent],
  templateUrl: './branch-detail.html',
  styleUrls: ['./branch-detail.scss']
})
export class BranchDetailComponent implements OnInit {
  private readonly branchService = inject(BranchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  orgId = signal<string>('');
  branch = signal<Branch | null>(null);
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    const orgId = this.route.snapshot.paramMap.get('orgId');
    const branchId = this.route.snapshot.paramMap.get('branchId');

    if (orgId && branchId) {
      this.orgId.set(orgId);
      this.loadBranch(branchId);
    } else {
      this.error.set('Organization ID or Branch ID not found in route.');
    }
  }

  loadBranch(id: string): void {
    this.isLoading.set(true);
    this.branchService.getBranchById(id).subscribe({
      next: (data) => {
        this.branch.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load branch details.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  onEdit(): void {
    if (this.branch()) {
      this.router.navigate(['/organizations', this.orgId(), 'branches', 'edit', this.branch()?.id]);
    }
  }
}
