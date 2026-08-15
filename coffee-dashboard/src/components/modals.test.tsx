import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MarkAsBackfillModal } from './log/MarkAsBackfillModal';
import { MarkDayEventModal } from './dashboard/MarkDayEventModal';
import { renderWithQuery } from '../test/renderWithQuery';
import * as statsApi from '../api/stats';
import type { MarkedDay } from '../api/types';

const existingEvent: MarkedDay = {
  date: '2026-08-15',
  kind: 'event',
  eventType: 'birthday',
  reason: 'Geburtstag',
  createdAt: '2026-08-15T06:00:00Z',
};

const massImportDay: MarkedDay = {
  date: '2026-08-15',
  kind: 'mass-import',
  eventType: null,
  reason: 'BSH Ausfall',
  createdAt: '2026-08-15T06:00:00Z',
};

describe('MarkAsBackfillModal', () => {
  beforeEach(() => {
    vi.spyOn(statsApi, 'addMarkedDay').mockResolvedValue(massImportDay);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders nothing while closed', () => {
    renderWithQuery(
      <MarkAsBackfillModal date="2026-08-15" displayDate="15.08.2026" open={false} onClose={vi.fn()} />,
    );

    expect(screen.queryByText('Als Massenimport markieren')).not.toBeInTheDocument();
  });

  it('rejects an empty reason', async () => {
    renderWithQuery(
      <MarkAsBackfillModal date="2026-08-15" displayDate="15.08.2026" open onClose={vi.fn()} />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Markieren' }));

    expect(statsApi.addMarkedDay).not.toHaveBeenCalled();
  });

  it('submits the reason and closes', async () => {
    const onClose = vi.fn();
    renderWithQuery(
      <MarkAsBackfillModal date="2026-08-15" displayDate="15.08.2026" open onClose={onClose} />,
    );

    await userEvent.type(screen.getByLabelText('Grund'), 'BSH Ausfall');
    await userEvent.click(screen.getByRole('button', { name: 'Markieren' }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(statsApi.addMarkedDay).toHaveBeenCalledWith({
      date: '2026-08-15',
      kind: 'mass-import',
      reason: 'BSH Ausfall',
    });
  });

  it('shows the API error and stays open', async () => {
    vi.mocked(statsApi.addMarkedDay).mockRejectedValue(new Error('Day already marked'));
    const onClose = vi.fn();
    renderWithQuery(
      <MarkAsBackfillModal date="2026-08-15" displayDate="15.08.2026" open onClose={onClose} />,
    );

    await userEvent.type(screen.getByLabelText('Grund'), 'Doppelt');
    await userEvent.click(screen.getByRole('button', { name: 'Markieren' }));

    expect(await screen.findByText('Day already marked')).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('closes via the backdrop', async () => {
    const onClose = vi.fn();
    renderWithQuery(
      <MarkAsBackfillModal date="2026-08-15" displayDate="15.08.2026" open onClose={onClose} />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Dialog schliessen' }));

    expect(onClose).toHaveBeenCalled();
  });
});

describe('MarkDayEventModal', () => {
  beforeEach(() => {
    vi.spyOn(statsApi, 'addMarkedDay').mockResolvedValue(existingEvent);
    vi.spyOn(statsApi, 'removeMarkedDay').mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('explains that mass-import days are managed on the log page', () => {
    renderWithQuery(
      <MarkDayEventModal
        date="2026-08-15"
        displayDate="Sa 15.08.2026"
        existing={massImportDay}
        open
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByText(/Massenimport-Markierungen/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Markieren/ })).not.toBeInTheDocument();
  });

  it('requires an event type before saving', async () => {
    renderWithQuery(
      <MarkDayEventModal date="2026-08-15" displayDate="Sa 15.08.2026" existing={null} open onClose={vi.fn()} />,
    );

    expect(screen.getByRole('button', { name: 'Markieren' })).toBeDisabled();
  });

  it('saves a new event annotation', async () => {
    const onClose = vi.fn();
    renderWithQuery(
      <MarkDayEventModal date="2026-08-15" displayDate="Sa 15.08.2026" existing={null} open onClose={onClose} />,
    );

    await userEvent.click(screen.getByRole('button', { name: /Feier/ }));
    await userEvent.type(screen.getByLabelText('Notiz (optional)'), 'Sommerfest');
    await userEvent.click(screen.getByRole('button', { name: 'Markieren' }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(statsApi.addMarkedDay).toHaveBeenCalledWith({
      date: '2026-08-15',
      kind: 'event',
      eventType: 'party',
      reason: 'Sommerfest',
    });
  });

  it('replaces an existing annotation by deleting first', async () => {
    renderWithQuery(
      <MarkDayEventModal
        date="2026-08-15"
        displayDate="Sa 15.08.2026"
        existing={existingEvent}
        open
        onClose={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Aktualisieren' }));

    await waitFor(() => expect(statsApi.removeMarkedDay).toHaveBeenCalledWith('2026-08-15'));
    expect(statsApi.addMarkedDay).toHaveBeenCalled();
  });

  it('removes an existing annotation', async () => {
    const onClose = vi.fn();
    renderWithQuery(
      <MarkDayEventModal
        date="2026-08-15"
        displayDate="Sa 15.08.2026"
        existing={existingEvent}
        open
        onClose={onClose}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /Entfernen/ }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(statsApi.removeMarkedDay).toHaveBeenCalledWith('2026-08-15');
  });

  it('surfaces a failed removal', async () => {
    vi.mocked(statsApi.removeMarkedDay).mockRejectedValue(new Error('Not marked'));
    renderWithQuery(
      <MarkDayEventModal
        date="2026-08-15"
        displayDate="Sa 15.08.2026"
        existing={existingEvent}
        open
        onClose={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /Entfernen/ }));

    expect(await screen.findByText('Not marked')).toBeInTheDocument();
  });
});
