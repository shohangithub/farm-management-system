import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { OrganizationService } from '../services/organization.service';
import { Organization } from '../models/organization.model';

@Component({
  selector: 'app-organization-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './organization-list.html',
  styleUrls: ['./organization-list.scss']
})
export class OrganizationListComponent implements OnInit {
  private readonly organizationService = inject(OrganizationService);
  
  organizations = signal<Organization[]>([]);
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadOrganizations();
  }

  loadOrganizations(): void {
    this.isLoading.set(true);
    this.organizationService.getOrganizations().subscribe({
      next: (data) => {
        this.organizations.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load organizations.');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  deactivate(id: string): void {
    if (confirm('Are you sure you want to deactivate this organization?')) {
      this.organizationService.deactivateOrganization(id).subscribe({
        next: () => this.loadOrganizations(),
        error: (err) => console.error(err)
      });
    }
  }
}
