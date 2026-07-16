import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BranchService } from '../../../organizations/services/branch.service';

@Component({
  selector: 'app-branch-widget',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './branch-widget.html',
  styleUrls: ['./branch-widget.scss']
})
export class BranchWidgetComponent implements OnInit {
  // In a real scenario, the dashboard might pass down the orgId or we get it from CurrentTenantState
  // For now, we will simulate a tenant/org selection or leave it abstract.
  private readonly branchService = inject(BranchService);

  totalBranches = signal<number>(0);
  activeBranches = signal<number>(0);
  isLoading = signal<boolean>(false);

  ngOnInit(): void {
    // This assumes the API or current tenant state provides a default organization context.
    // As a widget, it needs an orgId. We'll pass a dummy or load from a shared service later.
    // For demonstration, if we had an orgId:
    // this.loadStats('some-org-id');
  }

  loadStats(orgId: string): void {
    this.isLoading.set(true);
    this.branchService.getBranchesByOrganization(orgId).subscribe({
      next: (branches) => {
        this.totalBranches.set(branches.length);
        this.activeBranches.set(branches.filter(b => b.status === 1).length);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.isLoading.set(false);
      }
    });
  }
}
