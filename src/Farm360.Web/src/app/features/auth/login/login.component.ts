import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  readonly currentYear = new Date().getFullYear();

  loginForm = this.fb.nonNullable.group({
    phone: ['', [Validators.required]],
    password: ['', [Validators.required]]
  });

  isLoading = signal(false);
  errorMessage = signal('');
  successMessage = signal('');

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParams;
    if (qp['registered'] === 'true') {
      this.successMessage.set('🎉 Your account and farm trial have been created successfully! Please sign in.');
    }
    if (qp['phone']) {
      this.loginForm.patchValue({ phone: qp['phone'] });
    }
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');

    this.authService.login(this.loginForm.getRawValue()).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/';
        this.router.navigateByUrl(returnUrl).then(navigated => {
          if (!navigated) {
            this.isLoading.set(false);
          }
        });
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.detail || err.error?.title || 'Invalid credentials. Please try again.');
      }
    });
  }
}
