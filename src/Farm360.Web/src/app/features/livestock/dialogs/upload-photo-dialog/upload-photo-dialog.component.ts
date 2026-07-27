import { Component, inject, ChangeDetectionStrategy, signal, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AnimalService } from '../../services/animal.service';

export interface UploadPhotoDialogData {
  animalId: string;
  animalTag: string;
}

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB

@Component({
  selector: 'app-upload-photo-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './upload-photo-dialog.component.html'
})
export class UploadPhotoDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<UploadPhotoDialogComponent>);
  public readonly data = inject<UploadPhotoDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly animalSvc = inject(AnimalService);
  private readonly destroy$ = new Subject<void>();

  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  readonly form = this.fb.group({
    caption: ['', Validators.maxLength(200)]
  });

  readonly selectedFile = signal<File | null>(null);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly Math = Math;
  readonly MAX_FILE_SIZE = MAX_FILE_SIZE;

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      if (file.size > MAX_FILE_SIZE) {
        this.error.set(`File size exceeds the 5MB limit. (${(file.size / 1024 / 1024).toFixed(1)}MB)`);
        input.value = '';
        this.selectedFile.set(null);
        return;
      }
      this.error.set(null);
      this.selectedFile.set(file);
    }
  }

  submit(): void {
    const file = this.selectedFile();
    if (!file) {
      this.error.set("Please select an image file to upload.");
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const caption = this.form.get('caption')?.value || '';

    this.animalSvc.uploadPhotoFile(this.data.animalId, file, caption).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.submitting.set(false);
        if (err.error?.errors) {
            this.error.set(Object.values(err.error.errors).flat().join('\n'));
        } else {
            this.error.set(err.error?.detail || err.error?.title || 'Photo upload failed.');
        }
      }
    });
  }
}
