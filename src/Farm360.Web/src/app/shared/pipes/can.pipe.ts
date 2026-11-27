import { Pipe, PipeTransform, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

/**
 * `'animals.create' | can` → true when the current user holds the permission (or any of an array).
 * For places a structural `*hasPermission` cannot reach, such as a page header's
 * `[primaryActionLabel]` or an empty state's `[actionLabel]`:
 *
 *   [actionLabel]="('animals.create' | can) ? ('Register First Animal' | t) : undefined"
 *
 * Impure so it follows profile changes; the check is a short array scan. The API still enforces
 * every permission — this only hides actions a read-only user could not complete.
 */
@Pipe({ name: 'can', standalone: true, pure: false })
export class CanPipe implements PipeTransform {
  private readonly auth = inject(AuthService);

  transform(permission: string | readonly string[]): boolean {
    const codes = typeof permission === 'string' ? [permission] : permission;
    return this.auth.hasAnyPermission(codes);
  }
}
