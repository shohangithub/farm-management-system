import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

/**
 * Structural directive: renders its content only when the current user holds the permission
 * (or ANY of the permissions in an array). Re-evaluates when the user profile changes.
 *
 * Usage: `<button *hasPermission="'finance.delete'">Delete</button>`
 */
@Directive({
  selector: '[hasPermission]',
  standalone: true,
})
export class HasPermissionDirective {
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly auth = inject(AuthService);

  readonly hasPermission = input.required<string | string[]>();

  private rendered = false;

  constructor() {
    effect(() => {
      // Read the signal so the effect re-runs when the profile (and its permissions) changes.
      this.auth.currentUserSignal();
      const required = this.hasPermission();
      const codes = Array.isArray(required) ? required : [required];
      const allowed = this.auth.hasAnyPermission(codes);

      if (allowed && !this.rendered) {
        this.viewContainer.createEmbeddedView(this.templateRef);
        this.rendered = true;
      } else if (!allowed && this.rendered) {
        this.viewContainer.clear();
        this.rendered = false;
      }
    });
  }
}
