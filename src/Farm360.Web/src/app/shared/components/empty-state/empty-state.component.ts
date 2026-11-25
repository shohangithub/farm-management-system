import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, TranslatePipe],
  templateUrl: './empty-state.component.html'
})
export class EmptyStateComponent {
  @Input() icon: string = 'inbox';
  @Input() title: string = 'No Data Available';
  @Input() description: string = 'There is currently no data to display here.';
  @Input() actionLabel?: string;
  /** Icon on the action button; 'add' by default, empty for none. */
  @Input() actionIcon: string = 'add';
  
  @Output() action = new EventEmitter<void>();

  onAction(): void {
    this.action.emit();
  }
}
