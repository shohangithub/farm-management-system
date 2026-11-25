import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-farm-guidelines-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatDialogModule, TranslatePipe],
  templateUrl: './farm-guidelines-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FarmGuidelinesDialogComponent {
  constructor(public dialogRef: MatDialogRef<FarmGuidelinesDialogComponent>) {}
}
