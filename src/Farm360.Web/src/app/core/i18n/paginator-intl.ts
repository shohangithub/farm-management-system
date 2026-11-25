import { Injectable } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { translate } from './i18n.service';

/** Material paginator labels in the interface language. */
@Injectable()
export class TranslatedPaginatorIntl extends MatPaginatorIntl {
  override itemsPerPageLabel = translate('Items per page:');
  override nextPageLabel = translate('Next page');
  override previousPageLabel = translate('Previous page');
  override firstPageLabel = translate('First page');
  override lastPageLabel = translate('Last page');

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0 || pageSize === 0) return translate('0 of {total}', { total: length });
    const start = page * pageSize;
    const end = Math.min(start + pageSize, length);
    return translate('{from} – {to} of {total}', { from: start + 1, to: end, total: length });
  };
}
