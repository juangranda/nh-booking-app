import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from '../state/auth.store';

// Only our own API gets the token, so it never leaks to third-party hosts.
const API_URL = 'http://localhost:5170/api';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  const token = authStore.token;
  if (!token || !req.url.startsWith(API_URL)) {
    return next(req);
  }

  const authReq = req.clone({
    setHeaders: { Authorization: `Bearer ${token}` },
  });

  return next(authReq).pipe(
    catchError((err: unknown) => {
      // The token was rejected (expired or signed with an old key): log out.
      if (err instanceof HttpErrorResponse && err.status === 401) {
        authStore.clearSession();
        router.navigate(['/auth/login']);
      }
      return throwError(() => err);
    })
  );
};
