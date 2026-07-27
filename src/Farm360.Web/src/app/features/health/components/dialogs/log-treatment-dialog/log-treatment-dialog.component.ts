import { Component } from '@angular/core';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-log-treatment-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Log Treatment</h2>
    <mat-dialog-content>
      <p class="text-gray-500 text-sm mt-4">Form implementation pending.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" [mat-dialog-close]="true">Save</button>
    </mat-dialog-actions>
  `
})
export class LogTreatmentDialog {}
