import { describe, it, expect, vi, afterEach } from 'vitest';
import { getRange, formatDate, formatDisplayDate, today } from './dateUtils';

const SATURDAY_15_AUG_2026 = new Date(2026, 7, 15);

describe('getRange', () => {
  it('spans Monday to Sunday for the week period', () => {
    expect(getRange('week', SATURDAY_15_AUG_2026)).toEqual({
      from: '2026-08-10',
      to: '2026-08-16',
    });
  });

  it('spans the whole calendar month', () => {
    expect(getRange('month', SATURDAY_15_AUG_2026)).toEqual({
      from: '2026-08-01',
      to: '2026-08-31',
    });
  });

  it('spans the whole calendar year', () => {
    expect(getRange('year', SATURDAY_15_AUG_2026)).toEqual({
      from: '2026-01-01',
      to: '2026-12-31',
    });
  });

  it('starts at the data epoch for the all period', () => {
    expect(getRange('all', SATURDAY_15_AUG_2026)).toEqual({
      from: '2020-01-01',
      to: '2026-12-31',
    });
  });
});

describe('formatDate', () => {
  it('renders day and month of an ISO date key', () => {
    expect(formatDate('2026-08-15')).toBe('15.08.');
  });
});

describe('formatDisplayDate', () => {
  it('renders a German date from an ISO date key', () => {
    expect(formatDisplayDate('2026-08-15')).toBe('15.08.2026');
  });
});

describe('today', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns the current local date as an ISO date key', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 7, 15, 9, 30));
    expect(today()).toBe('2026-08-15');
  });
});
