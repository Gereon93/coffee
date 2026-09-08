import * as Sentry from '@sentry/react';
import { ErrorMessage } from './ErrorMessage';

interface Props {
  children: React.ReactNode;
}

export function SentryErrorBoundary({ children }: Readonly<Props>) {
  return (
    <Sentry.ErrorBoundary
      fallback={({ resetError }) => (
        <div className="flex flex-col items-center justify-center gap-4 p-6">
          <ErrorMessage message="Die Ansicht konnte nicht geladen werden." />
          <button
            type="button"
            onClick={resetError}
            className="rounded-md bg-red-800 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 dark:bg-red-200 dark:text-red-950 dark:hover:bg-red-300"
          >
            Erneut versuchen
          </button>
        </div>
      )}
    >
      {children}
    </Sentry.ErrorBoundary>
  );
}
