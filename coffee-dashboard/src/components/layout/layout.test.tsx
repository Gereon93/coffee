import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { NavBar } from './NavBar';
import { AppShell } from './AppShell';
import { CoffeePowerButton } from './CoffeePowerButton';
import { createTestQueryClient } from '../../test/renderWithQuery';
import * as coffeeApi from '../../api/coffee';
import * as timeLock from '../../lib/coffeeTimeLock';
import type { CoffeeStatus } from '../../api/types';

function renderInApp(ui: React.ReactElement, initialPath = '/') {
  return render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[initialPath]}>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );
}

function status(overrides: Partial<CoffeeStatus> = {}): CoffeeStatus {
  return {
    status: 'ok',
    reachable: true,
    powerState: 'off',
    operationState: 'inactive',
    label: 'Standby',
    lastUpdated: '2026-08-15T07:00:00Z',
    ...overrides,
  };
}

describe('CoffeePowerButton', () => {
  beforeEach(() => {
    vi.spyOn(timeLock, 'coffeeAllowed').mockReturnValue(true);
    vi.spyOn(coffeeApi, 'setCoffeePower').mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('offers switching on when the machine is off', async () => {
    vi.spyOn(coffeeApi, 'fetchCoffeeStatus').mockResolvedValue(status());
    renderInApp(<CoffeePowerButton />);

    const button = await screen.findByRole('button', { name: 'Kaffeemaschine: Einschalten' });
    await userEvent.click(button);

    await waitFor(() => expect(coffeeApi.setCoffeePower).toHaveBeenCalledWith('on'));
  });

  it('offers switching off when the machine is on', async () => {
    vi.spyOn(coffeeApi, 'fetchCoffeeStatus').mockResolvedValue(
      status({ powerState: 'on', operationState: 'ready', label: 'Bereit' }),
    );
    renderInApp(<CoffeePowerButton />);

    const button = await screen.findByRole('button', { name: 'Kaffeemaschine: Ausschalten' });
    await userEvent.click(button);

    await waitFor(() => expect(coffeeApi.setCoffeePower).toHaveBeenCalledWith('off'));
  });

  it('blocks interaction while brewing', async () => {
    vi.spyOn(coffeeApi, 'fetchCoffeeStatus').mockResolvedValue(
      status({ powerState: 'on', operationState: 'run', label: 'Brueht' }),
    );
    renderInApp(<CoffeePowerButton />);

    expect(await screen.findByRole('button', { name: 'Kaffeemaschine: Läuft' })).toBeDisabled();
  });

  it('locks the button outside coffee hours', async () => {
    vi.mocked(timeLock.coffeeAllowed).mockReturnValue(false);
    vi.spyOn(coffeeApi, 'fetchCoffeeStatus').mockResolvedValue(status());
    renderInApp(<CoffeePowerButton />);

    expect(await screen.findByRole('button', { name: 'Kaffeemaschine: Gesperrt' })).toBeDisabled();
  });

  it('falls back to manual buttons when the status is unreachable', async () => {
    vi.spyOn(coffeeApi, 'fetchCoffeeStatus').mockResolvedValue(
      status({ reachable: false, label: 'Offline', message: 'n8n antwortet nicht' }),
    );
    renderInApp(<CoffeePowerButton />);

    await userEvent.click(await screen.findByRole('button', { name: 'Kaffeemaschine einschalten' }));
    await waitFor(() => expect(coffeeApi.setCoffeePower).toHaveBeenCalledWith('on'));

    await userEvent.click(screen.getByRole('button', { name: 'Kaffeemaschine ausschalten' }));
    await waitFor(() => expect(coffeeApi.setCoffeePower).toHaveBeenCalledWith('off'));
  });
});

describe('NavBar', () => {
  beforeEach(() => {
    vi.spyOn(timeLock, 'coffeeAllowed').mockReturnValue(false);
    vi.spyOn(coffeeApi, 'fetchCoffeeStatus').mockResolvedValue(status());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('links to all three pages', () => {
    renderInApp(<NavBar isDarkMode={false} onToggleTheme={vi.fn()} />);

    expect(screen.getByRole('link', { name: 'Dashboard' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('link', { name: 'Heatmap' })).toHaveAttribute('href', '/heatmap');
    expect(screen.getByRole('link', { name: 'Log' })).toHaveAttribute('href', '/log');
  });

  it('toggles the theme', async () => {
    const onToggleTheme = vi.fn();
    renderInApp(<NavBar isDarkMode={false} onToggleTheme={onToggleTheme} />);

    await userEvent.click(screen.getByRole('button', { name: 'Zum dunklen Modus wechseln' }));

    expect(onToggleTheme).toHaveBeenCalled();
  });

  it('offers the light-mode switch while dark', () => {
    renderInApp(<NavBar isDarkMode onToggleTheme={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Zum hellen Modus wechseln' })).toBeInTheDocument();
  });
});

describe('AppShell', () => {
  beforeEach(() => {
    vi.spyOn(timeLock, 'coffeeAllowed').mockReturnValue(false);
    vi.spyOn(coffeeApi, 'fetchCoffeeStatus').mockResolvedValue(status());
    window.localStorage.clear();
    document.documentElement.classList.remove('dark');
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders the routed page inside the shell', () => {
    renderInApp(
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<p>Seiteninhalt</p>} />
        </Route>
      </Routes>,
    );

    expect(screen.getByText('Seiteninhalt')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'API Docs' })).toHaveAttribute('href', '/scalar/v1');
  });

  it('persists the chosen theme', async () => {
    renderInApp(
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<p>Seiteninhalt</p>} />
        </Route>
      </Routes>,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Zum dunklen Modus wechseln' }));

    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(window.localStorage.getItem('coffee-dashboard-theme')).toBe('dark');
  });

  it('restores a stored theme on mount', () => {
    window.localStorage.setItem('coffee-dashboard-theme', 'dark');

    renderInApp(
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<p>Seiteninhalt</p>} />
        </Route>
      </Routes>,
    );

    expect(screen.getByRole('button', { name: 'Zum hellen Modus wechseln' })).toBeInTheDocument();
  });
});
