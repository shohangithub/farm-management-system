import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { BreadcrumbComponent } from '../breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, BreadcrumbComponent],
  templateUrl: './page-header.component.html'
})
export class PageHeaderComponent {
  @Input() title: string = '';
  @Input() description: string = '';
  @Input() breadcrumbActiveNode?: string;
  @Input() primaryActionLabel?: string;
  @Input() primaryActionIcon?: string;
  @Input() secondaryActionLabel?: string;
  @Input() secondaryActionIcon?: string;
  
  @Output() primaryAction = new EventEmitter<void>();
  @Output() secondaryAction = new EventEmitter<void>();

  onPrimaryAction(): void {
    this.primaryAction.emit();
  }

  onSecondaryAction(): void {
    this.secondaryAction.emit();
  }
}
