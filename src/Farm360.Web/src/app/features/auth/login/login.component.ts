import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  readonly i18n = inject(I18nService);

  readonly currentYear = new Date().getFullYear();

  loginForm = this.fb.nonNullable.group({
    phone: ['', [Validators.required]],
    password: ['', [Validators.required]]
  });

  isLoading = signal(false);
  errorMessage = signal('');
  successMessage = signal('');

  /** "Explore the demo farm" is offered only when the demo is switched on and built. */
  demoAvailable = signal(false);
  isDemoLoading = signal(false);

  ngOnInit(): void {
    this.authService.getDemoStatus().subscribe(status => this.demoAvailable.set(status.enabled && status.ready));

    const qp = this.route.snapshot.queryParams;
    if (qp['registered'] === 'true') {
      this.successMessage.set(this.i18n.t('🎉 Your account and farm trial have been created successfully! Please sign in.'));
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
        this.errorMessage.set(err.error?.detail || err.error?.title || this.i18n.t('Invalid credentials. Please try again.'));
      }
    });
  }

  /** Signs in as the read-only demo account and opens the dashboard of the sample farm. */
  exploreDemo(): void {
    this.isDemoLoading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.authService.demoLogin().subscribe({
      next: () => {
        this.router.navigateByUrl('/dashboard').then(navigated => {
          if (!navigated) {
            this.isDemoLoading.set(false);
          }
        });
      },
      error: (err) => {
        this.isDemoLoading.set(false);
        if (err.status === 404) {
          this.demoAvailable.set(false);
        }
        this.errorMessage.set(err.error?.detail || this.i18n.t('The demo is being prepared, please try again in a minute.'));
      }
    });
  }
}
