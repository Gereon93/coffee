import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { SentryErrorBoundary } from './SentryErrorBoundary';

vi.mock('@sentry/react', async (importOriginal) => {
  const React = await import('react');
  const actual = await importOriginal<typeof import('@sentry/react')>();

  class MockErrorBoundary extends React.Component<{
    fallback: (data: { error: unknown; resetError: () => void }) => React.ReactNode;
    onError?: (error: unknown, info: { componentStack?: string }) => void;
    children: React.ReactNode;
  }> {
    state: { hasError: boolean; error: unknown } = { hasError: false, error: null };

    static getDerivedStateFromError(error: unknown) {
      return { hasError: true, error };
    }

    componentDidCatch(error: unknown, info: { componentStack?: string }) {
      this.props.onError?.(error, info);
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
    captureException: vi.fn(),
  };
});

function Thrower(): React.ReactNode {
  throw new Error('render error');
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
});
