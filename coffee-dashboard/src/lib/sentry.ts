import * as Sentry from '@sentry/react';

export function initSentry(): void {
  const dsn = import.meta.env.VITE_SENTRY_DSN;
  if (!dsn) {
    console.info('[sentry] disabled — no DSN');
    return;
  }

  const tracesSampleRate = Number(
    import.meta.env.VITE_SENTRY_TRACES_SAMPLE_RATE ?? 0,
  );

  Sentry.init({
    dsn,
    environment:
      import.meta.env.VITE_SENTRY_ENVIRONMENT ?? import.meta.env.MODE,
    release: import.meta.env.VITE_SENTRY_RELEASE ?? __BUILD_COMMIT__,
    sendDefaultPii: false,
    attachStacktrace: true,
    tracesSampleRate: Number.isFinite(tracesSampleRate) ? tracesSampleRate : 0,
    initialScope: {
      tags: { service: 'coffee-dashboard' },
    },
  });
}
