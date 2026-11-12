import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from './session';

export const authGuard: CanActivateFn = () => {
  const session = inject(SessionStore).session();
  if (session) return true;
  return inject(Router).createUrlTree(['/login']);
};
