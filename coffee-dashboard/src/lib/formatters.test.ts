import { describe, it, expect } from 'vitest';
import { formatNumber, formatHour, dayLabel } from './formatters';
import { emojiForEventType, EVENT_TYPE_META } from './eventTypeMeta';
import { coffeeAllowed } from './coffeeTimeLock';

describe('formatNumber', () => {
  it('groups thousands the German way', () => {
    expect(formatNumber(1234567)).toBe('1.234.567');
  });
});

describe('formatHour', () => {
  it('pads single-digit hours', () => {
    expect(formatHour(7)).toBe('07:00');
    expect(formatHour(18)).toBe('18:00');
  });
});

describe('dayLabel', () => {
  it('maps ISO weekday numbers to German short names', () => {
    expect(dayLabel(1)).toBe('Mo');
    expect(dayLabel(7)).toBe('So');
  });

  it('returns an empty label for out-of-range days', () => {
    expect(dayLabel(0)).toBe('');
    expect(dayLabel(9)).toBe('');
  });
});

describe('emojiForEventType', () => {
  it('returns the configured emoji for every known type', () => {
    for (const meta of EVENT_TYPE_META) {
      expect(emojiForEventType(meta.type)).toBe(meta.emoji);
    }
  });
});

describe('coffeeAllowed', () => {
  it('allows operation inside coffee hours', () => {
    expect(coffeeAllowed(new Date('2026-08-15T10:00:00Z'))).toBe(true);
  });

  it('blocks operation in the evening', () => {
    expect(coffeeAllowed(new Date('2026-08-15T20:00:00Z'))).toBe(false);
  });

  it('blocks operation before 07:00 Berlin time', () => {
    expect(coffeeAllowed(new Date('2026-08-15T03:00:00Z'))).toBe(false);
  });

  it('uses Berlin time, not UTC, at the winter boundary', () => {
    expect(coffeeAllowed(new Date('2026-01-15T06:30:00Z'))).toBe(true);
  });
});
