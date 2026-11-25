import { Pipe, PipeTransform } from '@angular/core';
import { translate, TranslationParams } from './i18n.service';

/**
 * `{{ 'Record Weight' | t }}` or, with values,
 * `{{ 'Showing {from} to {to} of {total} animals' | t: { from: a, to: b, total: n } }}`.
 * Pure: the language never changes without a page reload.
 */
@Pipe({ name: 't', standalone: true })
export class TranslatePipe implements PipeTransform {
  transform(text: string | null | undefined, params?: TranslationParams): string {
    return translate(text, params);
  }
}
