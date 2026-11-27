import { ApplicationConfig, provideZoneChangeDetection, provideAppInitializer, inject, LOCALE_ID } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeBn from '@angular/common/locales/bn';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { loadTranslations, readStoredLanguage } from './core/i18n/i18n.service';
import { TranslatedPaginatorIntl } from './core/i18n/paginator-intl';
import { provideRouter, withRouterConfig } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { languageInterceptor } from './core/interceptors/language.interceptor';
import { AuthService } from './core/services/auth.service';
import { provideEchartsCore } from 'ngx-echarts';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';

// Bangla month and day names for DatePipe when the interface is in Bangla. Digits stay Latin.
registerLocaleData(localeBn, 'bn');

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideAppInitializer(() => loadTranslations(readStoredLanguage())),
    provideAppInitializer(() => inject(AuthService).initializeSession()),
    { provide: LOCALE_ID, useFactory: () => (readStoredLanguage() === 'bn' ? 'bn' : 'en-US') },
    { provide: MatPaginatorIntl, useClass: TranslatedPaginatorIntl },
    provideRouter(routes, withRouterConfig({ onSameUrlNavigation: 'reload' })),
    provideHttpClient(withInterceptors([languageInterceptor, authInterceptor])),
    provideAnimations(),
    provideEchartsCore({ echarts: () => import('echarts') }),
    provideCharts(withDefaultRegisterables())
  ],
};
