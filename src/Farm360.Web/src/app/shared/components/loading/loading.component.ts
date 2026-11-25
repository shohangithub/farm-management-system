import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-loading',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule, TranslatePipe],
  templateUrl: './loading.component.html'
})
export class LoadingComponent {
  @Input() message: string = 'Loading data...';
  @Input() overlay: boolean = false;
}
