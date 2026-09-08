import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Sentry from '@sentry/react';
import { SentryErrorBoundary } from './SentryErrorBoundary';

vi.mock('@sentry/react', async (importOriginal) => {
  const React = await import('react');
  const actual = await importOriginal<typeof import('@sentry/react')>();
  const captureException = vi.fn();

  class MockErrorBoundary extends React.Component<{
    fallback: (data: { error: unknown; resetError: () => void }) => React.ReactNode;
    onError?: (error: unknown, componentStack: string | undefined, eventId: string) => void;
    children: React.ReactNode;
  }> {
    state: { hasError: boolean; error: unknown } = { hasError: false, error: null };

    static getDerivedStateFromError(error: unknown) {
      return { hasError: true, error };
    }

    componentDidCatch(error: unknown, info: { componentStack?: string }) {
      captureException(error);
      this.props.onError?.(error, info.componentStack, 'mock-event-id');
    }

    render() {
      if (this.state.hasError) {
        return this.props.fallback({
          error: this.state.error,
          resetError: () => this.setState({ hasError: false, error: null }),
        });
      }
      return this.props.children;
    }
  }

  return {
    ...actual,
    ErrorBoundary: MockErrorBoundary,
    captureException,
  };
});

function Thrower(): React.ReactNode {
  throw new Error('render error');
}

const flakyChild = { shouldThrow: true };

function FlakyChild(): React.ReactNode {
  if (flakyChild.shouldThrow) {
    throw new Error('one-time render error');
  }
  return <p>Erholter Inhalt</p>;
}

describe('SentryErrorBoundary', () => {
  it('renders children when no error is thrown', () => {
    render(
      <SentryErrorBoundary>
        <p>Inhalt</p>
      </SentryErrorBoundary>,
    );

    expect(screen.getByText('Inhalt')).toBeInTheDocument();
  });

  it('catches a render error and shows the fallback UI', () => {
    render(
      <SentryErrorBoundary>
        <Thrower />
      </SentryErrorBoundary>,
    );

    expect(screen.getByText('Die Ansicht konnte nicht geladen werden.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Erneut versuchen' })).toBeInTheDocument();
  });

  it('reports render errors to Sentry', () => {
    vi.mocked(Sentry.captureException).mockClear();

    render(
      <SentryErrorBoundary>
        <Thrower />
      </SentryErrorBoundary>,
    );

    expect(Sentry.captureException).toHaveBeenCalledOnce();
    expect(Sentry.captureException).toHaveBeenCalledWith(
      expect.objectContaining({ message: 'render error' }),
    );
  });

  it('offers a way back to the start page', () => {
    render(
      <SentryErrorBoundary>
        <Thrower />
      </SentryErrorBoundary>,
    );

    expect(screen.getByRole('link', { name: 'Zur Startseite' })).toHaveAttribute('href', '/');
  });

  it('recovers the children when retry succeeds after a one-time error', async () => {
    flakyChild.shouldThrow = true;
    render(
      <SentryErrorBoundary>
        <FlakyChild />
      </SentryErrorBoundary>,
    );

    expect(screen.getByText('Die Ansicht konnte nicht geladen werden.')).toBeInTheDocument();

    flakyChild.shouldThrow = false;
    await userEvent.click(screen.getByRole('button', { name: 'Erneut versuchen' }));

    expect(screen.getByText('Erholter Inhalt')).toBeInTheDocument();
    expect(screen.queryByText('Die Ansicht konnte nicht geladen werden.')).not.toBeInTheDocument();
  });
});
