import type { ReactNode } from 'react';
import { LoadingSpinner } from './LoadingSpinner';
import { ErrorMessage } from './ErrorMessage';

interface Props {
  isLoading: boolean;
  isError: boolean;
  children: ReactNode;
}

export function QueryBoundary({ isLoading, isError, children }: Readonly<Props>) {
  if (isLoading) return <LoadingSpinner />;
  if (isError) return <ErrorMessage />;
  return <>{children}</>;
}
