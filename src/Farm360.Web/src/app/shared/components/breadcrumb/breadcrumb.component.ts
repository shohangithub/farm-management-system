import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, NavigationEnd, ActivatedRoute } from '@angular/router';
import { filter } from 'rxjs/operators';
import { MatIconModule } from '@angular/material/icon';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [CommonModule, MatIconModule, RouterModule],
  templateUrl: './breadcrumb.component.html'
})
export class BreadcrumbComponent implements OnChanges {
  @Input() customLastNode?: string;
  breadcrumbs: Array<{ label: string, url: string }> = [];

  constructor(private router: Router, private activatedRoute: ActivatedRoute) {
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe(() => {
      this.breadcrumbs = this.buildBreadcrumb(this.activatedRoute.root);
    });
    // Build immediately on load
    this.breadcrumbs = this.buildBreadcrumb(this.activatedRoute.root);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['customLastNode'] && !changes['customLastNode'].firstChange) {
      this.breadcrumbs = this.buildBreadcrumb(this.activatedRoute.root);
    }
  }

  private buildBreadcrumb(route: ActivatedRoute, url: string = '', breadcrumbs: Array<{ label: string, url: string }> = []): Array<{ label: string, url: string }> {
    let label = route.routeConfig && route.routeConfig.data ? route.routeConfig.data['breadcrumb'] : '';
    let path = route.routeConfig && route.routeConfig.data ? route.routeConfig.path : '';

    // Auto-generate label from the path if no explicit breadcrumb data exists
    if (!label && route.routeConfig && route.routeConfig.path) {
      if (route.routeConfig.path !== '**') {
        const segments = route.routeConfig.path.split('/');
        const lastSegment = segments[segments.length - 1];

        // Skip dynamic parameter segments (e.g. :id, :branchId) — they will be
        // resolved to a human-readable name via `customLastNode` (breadcrumbActiveNode).
        if (!lastSegment.startsWith(':')) {
          // Convert kebab-case to Title Case for readability (e.g. "vet-visits" → "Vet Visits")
          label = lastSegment
            .split('-')
            .map((word: string) => word.charAt(0).toUpperCase() + word.slice(1))
            .join(' ');
        }
      }
    }

    const nextUrl = path ? `${url}/${path}` : url;

    if (label) {
      breadcrumbs.push({ label, url: nextUrl });
    }

    if (route.firstChild) {
      return this.buildBreadcrumb(route.firstChild, nextUrl, breadcrumbs);
    }

    // Override the very last node if customLastNode is provided by the page
    // (e.g. the animal Tag ID, incident title, protocol name, etc.)
    if (this.customLastNode && breadcrumbs.length > 0) {
      breadcrumbs[breadcrumbs.length - 1].label = this.customLastNode;
    }

    return breadcrumbs;
  }
}
