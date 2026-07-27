import { Component, inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-assign-protocol-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Assign Protocol</h2>
    <mat-dialog-content>
      <p>Assigning protocol: <strong>{{ data?.protocol?.title }}</strong></p>
      <p class="text-gray-500 text-sm mt-4">Form implementation pending.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" [mat-dialog-close]="true">Assign</button>
    </mat-dialog-actions>
  `
})
export class AssignProtocolDialog {
  data = inject(MAT_DIALOG_DATA);
}
