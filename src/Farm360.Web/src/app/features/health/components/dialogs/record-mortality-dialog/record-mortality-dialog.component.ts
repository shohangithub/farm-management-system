import { Component } from '@angular/core';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-record-mortality-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Record Mortality</h2>
    <mat-dialog-content>
      <p class="text-gray-500 text-sm mt-4">Form implementation pending.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="warn" [mat-dialog-close]="true">Record</button>
    </mat-dialog-actions>
  `
})
export class RecordMortalityDialog {}
