import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, MatIconModule],
  templateUrl: './register.component.html'
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  trialOptions = [
    { days: 3, label: '3 Days', desc: 'Quick test' },
    { days: 7, label: '7 Days', desc: 'Recommended', isRecommended: true },
    { days: 10, label: '10 Days', desc: 'Full trial' }
  ];

  registerForm = this.fb.group({
    name: ['', Validators.required],
    phone: ['', [Validators.required, Validators.pattern(/^[0-9+\-\s]+$/)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    farmName: ['', Validators.required],
    trialDays: [7, Validators.required]
  });

  isLoading = signal(false);
  error = signal<string | null>(null);

  onSubmit() {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.error.set(null);

    const val = this.registerForm.value;
    const payload = {
      fullName: val.name?.trim() || '',
      phone: val.phone?.trim() || '',
      email: val.email?.trim() || '',
      password: val.password || '',
      farmName: val.farmName?.trim() || '',
      trialDays: Number(val.trialDays) || 7
    };

    this.authService.register(payload).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/auth/login'], { 
          queryParams: { 
            registered: 'true',
            phone: payload.phone 
          } 
        });
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set(err.error?.detail || err.error?.message || err.message || 'Registration failed. Please try again.');
      }
    });
  }
}
