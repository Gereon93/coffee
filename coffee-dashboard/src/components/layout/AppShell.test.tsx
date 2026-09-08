import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, Link } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AppShell } from './AppShell';

vi.mock('@sentry/react', async (importOriginal) => {
  const React = await import('react');
  const actual = await importOriginal<typeof import('@sentry/react')>();

  class MockErrorBoundary extends React.Component<{
    fallback: (data: { error: unknown; resetError: () => void }) => React.ReactNode;
    children: React.ReactNode;
  }> {
    state: { hasError: boolean; error: unknown } = { hasError: false, error: null };

    static getDerivedStateFromError(error: unknown) {
      return { hasError: true, error };
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

vi.mock('../../hooks/useCoffeeStatus', () => ({
  useCoffeeStatus: () => ({ data: { power: 'on' }, isLoading: false, isError: false }),
  useSetCoffeePower: () => ({ mutate: vi.fn(), isPending: false }),
}));

function BrokenPage(): React.ReactNode {
  throw new Error('broken page');
}

function HealthyPage(): React.ReactNode {
  return <p>Gesunde Seite</p>;
}

describe('AppShell route error boundary', () => {
  it('recovers when navigating to another route after a page render error', async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/broken']}>
          <Link to="/healthy">Zur gesunden Route</Link>
          <Routes>
            <Route element={<AppShell />}>
              <Route path="broken" element={<BrokenPage />} />
              <Route path="healthy" element={<HealthyPage />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(screen.getByText('Die Ansicht konnte nicht geladen werden.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('link', { name: 'Zur gesunden Route' }));

    expect(screen.getByText('Gesunde Seite')).toBeInTheDocument();
    expect(screen.queryByText('Die Ansicht konnte nicht geladen werden.')).not.toBeInTheDocument();
  });
});
