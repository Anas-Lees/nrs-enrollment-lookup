import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Attaches a unique X-Correlation-Id to each outgoing API request so a user action can
 * be traced from the browser through the API to its logs/traces. The backend reuses this
 * id when present (and echoes it back).
 */
export const correlationInterceptor: HttpInterceptorFn = (req, next) => {
  const correlationId =
    globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;

  return next(req.clone({ setHeaders: { 'X-Correlation-Id': correlationId } }));
};
