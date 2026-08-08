import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';

@Component({
  selector: 'app-farm-guidelines-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatDialogModule],
  templateUrl: './farm-guidelines-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FarmGuidelinesDialogComponent {
  constructor(public dialogRef: MatDialogRef<FarmGuidelinesDialogComponent>) {}
}
