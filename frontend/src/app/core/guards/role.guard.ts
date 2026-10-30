import { Injectable } from '@angular/core';
import { AuthStore } from '../state/auth.store';
import { CanActivate, Router, UrlTree } from '@angular/router';
import UserRoleModel from '../../features/auth/domain/models/role.model';

@Injectable({ providedIn: 'root' })
export class RoleGuard implements CanActivate {

  constructor(
    private authStore: AuthStore,
    private router: Router
  ) { }

  // Non-admins go back to the booking area; there is no "not authorized" page.
  canActivate(): boolean | UrlTree {
    const role = this.authStore.getCurrentRole();
    if (role === UserRoleModel.ADMIN) return true;

    return this.router.createUrlTree(['/booking']);
  }

}
