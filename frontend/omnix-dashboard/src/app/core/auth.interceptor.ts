import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SessionStore } from './session';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(SessionStore).session();
  const router = inject(Router);
  const store = inject(SessionStore);
  const authed = session
    ? req.clone({ setHeaders: { Authorization: `Bearer ${session.token}` } })
    : req;

  return next(authed).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !req.url.includes('/auth/login') &&
        !req.url.includes('/auth/register')
      ) {
        store.clear();
        void router.navigateByUrl('/login');
      }
      return throwError(() => error);
    }),
  );
};

export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { error?: string } | null;
    if (body && typeof body.error === 'string') return body.error;
    if (error.status === 0) return 'The API is not reachable.';
  }
  return 'Request failed.';
}
