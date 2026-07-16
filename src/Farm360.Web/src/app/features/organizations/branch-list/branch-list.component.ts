import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { BranchService } from '../services/branch.service';
import { BranchList } from '../models/branch.model';

@Component({
  selector: 'app-branch-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './branch-list.html',
  styleUrls: ['./branch-list.scss']
})
export class BranchListComponent implements OnInit {
  private readonly branchService = inject(BranchService);
  private readonly route = inject(ActivatedRoute);

  orgId = signal<string>('');
  branches = signal<BranchList[]>([]);
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    const orgId = this.route.snapshot.paramMap.get('orgId');
    if (orgId) {
      this.orgId.set(orgId);
      this.loadBranches();
    } else {
      this.error.set('Organization ID not found in route.');
    }
  }

  loadBranches(): void {
    this.isLoading.set(true);
    this.branchService.getBranchesByOrganization(this.orgId()).subscribe({
      next: (data) => {
        this.branches.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load branches.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  delete(id: string): void {
    if (confirm('Are you sure you want to delete this branch?')) {
      this.branchService.deleteBranch(id).subscribe({
        next: () => this.loadBranches(),
        error: (err) => console.error(err)
      });
    }
  }
}
