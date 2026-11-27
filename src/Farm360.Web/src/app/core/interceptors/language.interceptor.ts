import { HttpInterceptorFn } from '@angular/common/http';
import { readStoredLanguage } from '../i18n/i18n.service';

/**
 * Tells the API which language to write its messages in (`Accept-Language: bn` or `en`), so
 * error and validation messages come back in the language the interface is shown in.
 * The server changes only message text, never number or date formats.
 */
export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/') || req.headers.has('Accept-Language')) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { 'Accept-Language': readStoredLanguage() } }));
};
